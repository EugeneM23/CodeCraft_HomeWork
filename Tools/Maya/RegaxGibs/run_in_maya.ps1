param([string]$Script)
# Runs a python file inside Maya (commandPort :7005); prints what it printed.
# Maya's port runs every command it receives twice, so each run carries an id and the second
# pass sees it already done.
$log = [IO.Path]::ChangeExtension($Script, ".log")
if ([IO.File]::Exists($log)) { [IO.File]::Delete($log) }
$p = $Script.Replace('\', '/'); $l = $log.Replace('\', '/')
$id = [Guid]::NewGuid().ToString('N')
$cmd = "exec(""import builtins,sys,io,traceback\n_d=builtins.__dict__.setdefault('_claude_runs',set())\nif '$id' not in _d:\n _d.add('$id')\n _b=io.StringIO();_o=sys.stdout;sys.stdout=_b\n try:\n  exec(compile(open(r'$p',encoding='utf-8-sig').read(),r'$p','exec'),{'__name__':'__main__'})\n except Exception:\n  traceback.print_exc(file=_b)\n finally:\n  sys.stdout=_o\n  open(r'$l','w',encoding='utf-8').write(_b.getvalue())"")`n"
$c = New-Object Net.Sockets.TcpClient
$c.Connect('127.0.0.1', 7005)
$s = $c.GetStream(); $s.ReadTimeout = 600000
$bytes = [Text.Encoding]::UTF8.GetBytes($cmd)
$s.Write($bytes, 0, $bytes.Length)
$b = New-Object byte[] 65536
try { $null = $s.Read($b, 0, $b.Length) } catch {}
$c.Close()
for ($i = 0; $i -lt 600 -and -not [IO.File]::Exists($log); $i++) { Start-Sleep -Milliseconds 200 }
Get-Content -LiteralPath $log -Raw -Encoding utf8
