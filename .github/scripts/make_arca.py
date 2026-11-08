"""Build the Arca Live copy of the project site: same page, no download or repository links.

Usage: python3 .github/scripts/make_arca.py <site index.html> <output arca/index.html>
Run by the Pages workflow, so the Arca page always matches the main page.
"""
import re
import sys

src, out = sys.argv[1], sys.argv[2]
html = open(src, encoding="utf-8").read()
REPO = "https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-"


def cut(pattern, repl="", count=1):
    global html
    new, n = re.subn(pattern, repl, html, count=count, flags=re.S)
    if n != count:
        sys.exit(f"make_arca: pattern matched {n} times, expected {count}: {pattern[:80]}")
    html = new


# header GitHub icon
cut(r'\s*<a class="gh" href="' + re.escape(REPO) + r'"[^>]*>.*?</a>')
# hero: download and release-notes buttons (keep "how to install")
cut(r'\s*<a class="btn primary" href="' + re.escape(REPO) + r'/releases/latest">.*?</a>')
cut(r'\s*<a class="btn" href="' + re.escape(REPO) + r'/releases">.*?</a>')
# bug reports: issues -> comments
cut(r'<a href="' + re.escape(REPO) + r'/issues">GitHub 이슈</a>에 올려 주세요\.', "댓글로 로그 파일과 함께 남겨 주세요.")
cut(r'Please open a <a href="' + re.escape(REPO) + r'/issues">GitHub issue</a>\.', "Please leave a comment with your log file.")
# footer: GitHub / changelog / releases links
cut(r'\s*<span><a href="' + re.escape(REPO) + r'">GitHub</a> · .*?Releases</span></a></span>')

# the page lives one folder down
html = html.replace('="img/', '="../img/').replace('href="style.css"', 'href="../style.css"').replace('src="app.js"', 'src="../app.js"')
html = html.replace("<head>\n", '<head>\n<meta name="robots" content="noindex">\n', 1)

for bad in (REPO + '"', REPO + "/releases", REPO + "/issues", REPO + "/tree"):
    if bad in html:
        sys.exit(f"make_arca: link left in page: {bad}")
open(out, "w", encoding="utf-8").write(html)
print(f"make_arca: wrote {out} ({len(html)} bytes)")
