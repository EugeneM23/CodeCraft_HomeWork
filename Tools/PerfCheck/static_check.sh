#!/usr/bin/env bash
# Static project check against Docs/Rules/Performance.md (section 5) and Docs/Rules/Project.md.
#
# Usage: Tools/PerfCheck/static_check.sh [--repo <path>] [--base <ref>]
#   --repo  working copy to check (default: current directory)
#   --base  ref the branch is compared with; hits in lines/files added since it are NEW (blocking),
#           everything else is DEBT. Default: merge-base with game/develop, or none when HEAD is
#           game/develop itself (then everything is debt).
#
# The working tree is checked as it is, uncommitted and untracked files included.
# A line that ends with `// perf-ok: <reason>` is exempt from the code-pattern rules.
# Output: Markdown on stdout, and a last line `STATIC_JSON {...}` with the counts per rule.

set -u
REPO="."
BASE=""
BASE_GIVEN=0
while [ $# -gt 0 ]; do
  case "$1" in
    --repo) REPO="$2"; shift 2 ;;
    --base) BASE="$2"; BASE_GIVEN=1; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done
cd "$REPO" || exit 2

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

if [ $BASE_GIVEN -eq 0 ] && git rev-parse -q --verify game/develop >/dev/null; then
  if [ "$(git rev-parse HEAD)" != "$(git rev-parse game/develop)" ]; then
    BASE=$(git merge-base HEAD game/develop)
  fi
fi

# --- What the branch added: "path:line" for added lines, and a list of added files. ---
: > "$TMP/added_lines"
: > "$TMP/new_files"
if [ -n "$BASE" ]; then
  git diff -U0 --no-color --no-renames "$BASE" -- Assets Tools Docs .claude 2>/dev/null | awk '
    /^\+\+\+ b\// { path = substr($0, 7); next }
    /^@@/ {
      split($3, a, ","); start = substr(a[1], 2); n = (a[2] == "" ? 1 : a[2])
      for (i = 0; i < n; i++) print path ":" (start + i)
    }' > "$TMP/added_lines"
  git diff --name-only --no-renames --diff-filter=A "$BASE" -- . > "$TMP/new_files"
fi
git ls-files --others --exclude-standard >> "$TMP/new_files"
# Untracked files are new as a whole: every line counts as added.
git ls-files --others --exclude-standard -- '*.cs' | while IFS= read -r f; do
  awk -v p="$f" '{ print p ":" NR }' "$f"
done >> "$TMP/added_lines"
sort -u -o "$TMP/added_lines" "$TMP/added_lines"
sort -u -o "$TMP/new_files" "$TMP/new_files"

JSON=""
TOTAL_NEW=0
TOTAL_DEBT=0
add_json() { JSON="$JSON\"$1\":{\"new\":$2,\"debt\":$3},"; TOTAL_NEW=$((TOTAL_NEW + $2)); TOTAL_DEBT=$((TOTAL_DEBT + $3)); }

# Splits "path:line:text" hits into NEW (line added by the branch) and DEBT.
split_hits() { # $1 = hits file
  awk -F: 'FILENAME == ARGV[1] { added[$0] = 1; next } { key = $1 ":" $2; if (key in added) print > "'"$TMP"'/new"; else print > "'"$TMP"'/debt" }' \
    "$TMP/added_lines" "$1"
}

# Splits a list of paths into NEW (file added by the branch) and DEBT.
split_files() { # $1 = paths file
  awk 'FILENAME == ARGV[1] { added[$0] = 1; next } { if ($0 in added) print > "'"$TMP"'/new"; else print > "'"$TMP"'/debt" }' \
    "$TMP/new_files" "$1"
}

report() { # $1 = id, $2 = title, $3 = examples of debt to show
  local n d
  n=$( [ -f "$TMP/new" ] && wc -l < "$TMP/new" || echo 0); n=$((n + 0))
  d=$( [ -f "$TMP/debt" ] && wc -l < "$TMP/debt" || echo 0); d=$((d + 0))
  add_json "$1" "$n" "$d"
  if [ "$n" -eq 0 ] && [ "$d" -eq 0 ]; then
    echo "- \`$1\` $2: чисто"
  else
    echo "- \`$1\` $2: **новых $n**, долг $d"
    [ "$n" -gt 0 ] && sed 's/^/    - NEW `/; s/$/`/' "$TMP/new" | cut -c1-220
    [ "$d" -gt 0 ] && [ "$3" -gt 0 ] && head -n "$3" "$TMP/debt" | sed 's/^/    - долг `/; s/$/`/' | cut -c1-220
  fi
  rm -f "$TMP/new" "$TMP/debt"
}

# Runtime game code: no editor tools, tests or demos.
RUNTIME=(':(glob)Assets/Game/**/*.cs' ':(exclude,glob)Assets/Game/**/Editor/**' ':(exclude,glob)Assets/Game/**/Tests/**'
  ':(exclude,glob)Assets/Game/Tests/**' ':(exclude,glob)Assets/Game/Voxels/Demo/**' ':(exclude,glob)Assets/**/*~/**')

code_rule() { # $1 = id, $2 = title, $3 = extended regex, $4 = optional regex of hits to drop
  git grep --untracked -n -I -E "$3" -- "${RUNTIME[@]}" 2>/dev/null | grep -v 'perf-ok:' | grep -v -E "${4:-^$^}" > "$TMP/hits"
  # Skip comment-only lines.
  awk -F: '{ t = $0; sub(/^[^:]*:[^:]*:[ \t]*/, "", t); if (t !~ /^(\/\/|\*|\/\*)/) print }' "$TMP/hits" > "$TMP/hits2"
  split_hits "$TMP/hits2"
  report "$1" "$2" 3
}

echo "## Статическая проверка"
echo
echo "- Ветка: \`$(git rev-parse --abbrev-ref HEAD)\` @ \`$(git rev-parse --short HEAD)\`"
if [ -n "$BASE" ]; then echo "- База для «новых»: \`$(git rev-parse --short "$BASE")\`"; else echo "- База не задана: всё найденное — долг"; fi
echo "- Незакоммиченных изменений: $(git status --porcelain | wc -l)"
echo

echo "### Код (Performance.md, раздел 5)"
code_rule linq          "LINQ в рантайме"                         '^\s*using System\.Linq'
code_rule unity-update  "Update/LateUpdate/FixedUpdate у MonoBehaviour" '\bvoid\s+(Update|LateUpdate|FixedUpdate)\s*\(' '\bstatic\b'
code_rule find          "Find*/FindObject* в рантайме"            '\b(GameObject\.Find|FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType|FindWithTag|FindGameObjectsWithTag)\b'
code_rule camera-main   "Camera.main"                             '\bCamera\.main\b'
code_rule instantiate   "Instantiate (в бою — только пулы)"       '(^|[^_.A-Za-z]|\bObject\.)Instantiate\s*[<(]'
code_rule old-input     "старый Input вместо Input System"        '(^|[^A-Za-z_.])Input\.(Get[A-Za-z]*|mousePosition|anyKey|mouseScrollDelta|touches|touchCount)\b'
code_rule alloc-physics "физ. запросы с аллокацией"               '\bPhysics2?D?\.(RaycastAll|SphereCastAll|CapsuleCastAll|BoxCastAll|OverlapSphere|OverlapBox|OverlapCapsule)\s*\('
code_rule atomic-alloc  "GetBehaviours()/GetBehaviourEnumerator()" '\.(GetBehaviours\s*(<[^>]*>)?\s*\(|GetBehaviourEnumerator\s*\()'
code_rule debug-log     "Debug.Log в рантайме"                    '\bDebug\.Log\s*\('
code_rule ongui         "OnGUI (только отладка и демо)"           '\bvoid\s+OnGUI\s*\('

# using UnityEditor without #if UNITY_EDITOR, in runtime code.
: > "$TMP/hits"
git grep --untracked -l -E '^\s*using UnityEditor' -- "${RUNTIME[@]}" 2>/dev/null | while IFS= read -r f; do
  awk -v p="$f" '/^\s*#if[^\n]*UNITY_EDITOR/ { inif++ } /^\s*#endif/ { if (inif > 0) inif-- }
    /^\s*using UnityEditor/ && inif == 0 { print p ":" NR ":" $0 }' "$f"
done > "$TMP/hits"
split_hits "$TMP/hits"; report editor-in-runtime "using UnityEditor без #if UNITY_EDITOR" 3
echo

echo "### Нейминг кода (Project.md, раздел 2)"
# File name must match a declared type; *Jobs.cs and *Types.cs are allowed bundles.
git ls-files --cached --others --exclude-standard -- ':(glob)Assets/Game/**/*.cs' > "$TMP/cs"
tr '\n' '\0' < "$TMP/cs" | xargs -0 awk '
  FNR == 1 { if (prev != "" && !found && !bundle) print prev; prev = FILENAME; n = FILENAME; sub(/.*\//, "", n); sub(/\.cs$/, "", n)
             found = 0; bundle = (n ~ /(Jobs|Types)$/) }
  { if (match($0, "(class|struct|interface|enum|record)[ \t]+" n "([^A-Za-z0-9_]|$)")) found = 1 }
  END { if (prev != "" && !found && !bundle) print prev }' > "$TMP/hits"
split_files "$TMP/hits"; report file-type-name "имя файла не совпадает с типом" 5

tr '\n' '\0' < "$TMP/cs" | xargs -0 awk 'FNR == 1 { if (prev != "" && !ns) print prev; prev = FILENAME; ns = 0 }
  /^\s*namespace\s/ { ns = 1 } END { if (prev != "" && !ns) print prev }' > "$TMP/hits"
git ls-files --cached --others --exclude-standard -- ':(glob)Assets/*.cs' >> "$TMP/hits"
split_files "$TMP/hits"; report no-namespace "файл без namespace или вне Assets/Game" 5

# Private fields: _camelCase (instance) or s_camelCase (static). Constants and static readonly skipped.
git grep --untracked -n -E '^\s*(\[[^]]*\]\s*)*private\s+(readonly\s+)?[A-Za-z_][]A-Za-z0-9_<>,.?[ ]*\s+[A-Za-z][A-Za-z0-9]*\s*(=|;)' \
  -- ':(glob)Assets/Game/**/*.cs' 2>/dev/null | grep -v -E '\b(const|static)\b' > "$TMP/hits"
split_hits "$TMP/hits"; report field-naming "приватное поле не _camelCase" 3

git grep --untracked -n -E '\b(class|interface)\s+[A-Za-z0-9_]*(Manager|Helper|Utils|Util)\b' -- ':(glob)Assets/Game/**/*.cs' 2>/dev/null > "$TMP/hits"
split_hits "$TMP/hits"; report banned-suffix "суффиксы Manager/Helper/Utils" 3
echo

echo "### Ассеты (Project.md, раздел 3)"
git ls-files --cached --others --exclude-standard -- Assets/Game Assets/Modules | grep -v -E '\.meta$|~/' > "$TMP/assets"

grep -E '[ ()]' "$TMP/assets" > "$TMP/hits"
split_files "$TMP/hits"; report asset-chars "пробелы или скобки в имени" 5

# Prefixes; Sprites/, Fonts/ and Modules/ are not prefixed.
grep '^Assets/Game/' "$TMP/assets" | grep -v -E '^Assets/Game/(Sprites|Fonts)/' | awk -F/ '
  { name = $NF; ext = tolower(name); sub(/.*\./, "", ext)
    if (ext == "mat" && name !~ /^MT_/) print
    else if ((ext == "png" || ext == "tga" || ext == "psd" || ext == "jpg" || ext == "jpeg" || ext == "exr" || ext == "tif" || ext == "tiff") && name !~ /^T_/) print
    else if ((ext == "fbx" || ext == "obj" || ext == "blend") && name !~ /^(SM|SK)_/) print
    else if ((ext == "controller" || ext == "overridecontroller") && name !~ /^AC_/) print }' > "$TMP/hits"
split_files "$TMP/hits"; report asset-prefix "нет префикса типа (MT_/T_/SM_/SK_/AC_)" 3

grep -i -E '(^|/)(\{[^/]*|New Folder[^/]*|Copy of [^/]*|New Material[^/]*|[^/]* [0-9]+\.[a-z]+|[^/]*\([0-9]+\)[^/]*)(/|$)' "$TMP/assets" > "$TMP/hits"
# Same on disk: folders git does not see because they hold only ignored or no files.
find Assets -type d \( -name '{*' -o -iname 'New Folder*' -o -iname 'Copy of*' \) 2>/dev/null >> "$TMP/hits"
sort -u -o "$TMP/hits" "$TMP/hits"
split_files "$TMP/hits"; report junk-names "мусорные имена и папки-опечатки" 10

find Assets/Game Assets/Modules -type d -empty 2>/dev/null > "$TMP/hits"
split_files "$TMP/hits"; report empty-dirs "пустые папки" 10

git ls-files --cached --others --exclude-standard -- 'Assets/*' | grep -v -E '^Assets/[^/]+/' | grep -v '\.meta$' > "$TMP/hits"
split_files "$TMP/hits"; report assets-root "файлы в корне Assets/" 10

git ls-files -- . | grep -E '^(mono_crash\.[^/]*|\.idea/.*)$' > "$TMP/hits"
split_files "$TMP/hits"; report repo-root "мусор в корне репозитория (mono_crash, .idea)" 3

# .meta without asset, asset without .meta (Assets/Game and Assets/Modules).
git ls-files --cached --others --exclude-standard -- Assets/Game Assets/Modules | grep -v '~/' > "$TMP/all"
awk '/\.meta$/ { m[substr($0, 1, length($0) - 5)] = 1; next } { a[$0] = 1; d = $0
       while (sub(/\/[^\/]*$/, "", d)) if (d ~ /\//) dir[d] = 1 }
     END { for (k in m) if (!(k in a) && !(k in dir)) print k ".meta (нет ассета)"
           for (k in a) if (!(k in m)) print k " (нет .meta)" }' "$TMP/all" | sort > "$TMP/hits"
awk '{ sub(/ \(.*$/, ""); print }' "$TMP/hits" > "$TMP/hits_paths"
split_files "$TMP/hits_paths"; report meta "рассинхрон .meta" 10

git ls-tree -r -l HEAD | awk -F'	' '{ split($1, m, / +/); if (m[4] > 52428800) print $2 }' > "$TMP/hits"
split_files "$TMP/hits"; report large-files "файлы больше 50 МБ в git" 5
echo

echo "### Лишние ассеты (кандидаты на удаление)"
# An asset is used when its GUID is referenced from a scene/prefab/asset, it is under Resources/,
# it is a scene in Build Settings, or its file name appears in code. Shaders and code are skipped.
git grep --untracked -h -o -E '[0-9a-f]{32}' -- '*.unity' '*.prefab' '*.asset' '*.mat' '*.controller' \
  '*.overrideController' '*.anim' '*.playable' '*.mixer' '*.shadergraph' '*.shadersubgraph' '*.spriteatlas' \
  '*.lighting' '*.physicMaterial' '*.terrainlayer' '*.vfx' '*.preset' '*.mask' 'ProjectSettings/*' 2>/dev/null \
  | sort -u > "$TMP/used_guids"
git grep --untracked -h -o -E '"[A-Za-z0-9_ ./-]+"' -- '*.cs' 2>/dev/null | tr -d '"' | sed 's#.*/##' | sort -u > "$TMP/code_names"
grep '^Assets/Game/' "$TMP/assets" | grep -v -E '/Resources/|\.(cs|asmdef|asmref|shader|hlsl|cginc|compute|md|txt|json|dll|rsp)$|/Tests/' > "$TMP/cands"
: > "$TMP/hits"
while IFS= read -r f; do
  [ -f "$f.meta" ] || continue
  g=$(grep -m1 -o -E '^guid: [0-9a-f]{32}' "$f.meta" | cut -c7-)
  [ -z "$g" ] && continue
  printf '%s\t%s\n' "$g" "$f"
done < "$TMP/cands" > "$TMP/cand_guids"
awk -F'\t' 'FILENAME == ARGV[1] { used[$0] = 1; next } FILENAME == ARGV[2] { code[$0] = 1; next }
  { n = $2; sub(/.*\//, "", n); b = n; sub(/\.[^.]*$/, "", b)
    if (!($1 in used) && !(n in code) && !(b in code)) print $2 }' "$TMP/used_guids" "$TMP/code_names" "$TMP/cand_guids" \
  | sort > "$TMP/hits"
split_files "$TMP/hits"; report unused-assets "ассет без ссылок (по GUID и именам в коде)" 25
echo

echo "### Итог"
echo "- Новых нарушений: **$TOTAL_NEW** (блокируют влитие)"
echo "- Долг: $TOTAL_DEBT"
echo
echo "STATIC_JSON {\"kind\":\"static\",\"date\":\"$(date '+%Y-%m-%d %H:%M')\",\"base\":\"$( [ -n "$BASE" ] && git rev-parse --short "$BASE" || echo none)\",\"branch\":\"$(git rev-parse --abbrev-ref HEAD)\",\"commit\":\"$(git rev-parse --short HEAD)\",\"new\":$TOTAL_NEW,\"debt\":$TOTAL_DEBT,\"rules\":{${JSON%,}}}"
