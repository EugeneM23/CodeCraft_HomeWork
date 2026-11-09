use strict; use warnings; use utf8;
binmode(STDOUT, ':encoding(UTF-8)');

# map.pl <big-document.md> > MAP.txt
#
# Снимает карту заголовков собранного руководства: диапазон строк и размер
# каждой части, главы и раздела. Размер — в СИМВОЛАХ, не в байтах (кириллица в
# UTF-8 занимает два байта на символ, так что на диске файл вдвое больше).
# Каждая строка помечена якорем главы, поэтому один grep по 'ch-06' отдаёт и
# саму главу, и все её разделы.
#
# Перезапускать ПОСЛЕ каждой пересборки документа — иначе диапазоны соврут.

my $file = $ARGV[0] or die "usage: perl map.pl <document.md> > MAP.txt\n";
open my $fh, '<:encoding(UTF-8)', $file or die "$file: $!";

my ($in_fence, $n, $scope) = (0, 0, 'doc');
my (@h, @len);
while (my $l = <$fh>) {
  $n++;
  push @len, length($l);
  if ($l =~ /^```/)                   { $in_fence = !$in_fence; next }
  next if $in_fence;
  if ($l =~ /<a id="((?:ch|part)-\d+)/) { $scope = $1; next }
  if ($l =~ /^(#{1,3})\s+(.*?)\s*$/)  { push @h, [$n, length($1), $2, $scope] }
}
close $fh;

my $total = $n;
my @cum = (0);
$cum[$_ + 1] = $cum[$_] + $len[$_] for 0 .. $#len;   # cum[i] = байт до строки i+1

sub size_of {
  my ($a, $b) = @_;
  my $bytes = $cum[$b] - $cum[$a - 1];
  return $bytes < 1024 ? "${bytes}B" : int($bytes / 1024) . 'K';
}

for my $i (0 .. $#h) {
  my ($ln, $lv, $tx, $sc) = @{ $h[$i] };
  my $end = $total;
  for my $j ($i + 1 .. $#h) {
    if ($h[$j][1] <= $lv) { $end = $h[$j][0] - 1; last }
  }
  printf("%-8s H%d  L%-6s %-5s %s%s\n",
         $sc, $lv, "$ln-$end", size_of($ln, $end), ('  ' x ($lv - 1)), $tx);
}
