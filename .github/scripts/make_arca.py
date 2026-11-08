"""Build the Arca Live copy of the project site: same page without the download, release and GitHub buttons
(like the Tushonka Cleaner Arca page). Everything else, including bug-report and changelog links, stays.

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
# footer: keep the changelog link, drop GitHub and Releases
cut(r'<a href="' + re.escape(REPO) + r'">GitHub</a> · ')
cut(r' · <a href="' + re.escape(REPO) + r'/releases"><span lang="ko">릴리즈</span><span lang="en">Releases</span></a>')

# the page lives one folder down
html = html.replace('="img/', '="../img/').replace('href="style.css"', 'href="../style.css"').replace('src="app.js"', 'src="../app.js"')

for bad in (REPO + '"', REPO + "/releases"):
    if bad in html:
        sys.exit(f"make_arca: link left in page: {bad}")
open(out, "w", encoding="utf-8").write(html)
print(f"make_arca: wrote {out} ({len(html)} bytes)")
