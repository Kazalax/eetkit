#!/usr/bin/env python3
"""Builds the GitHub Pages site: the home page from README.md and one page per article in site/clanky.

The home page is the README itself, so the site never says more than the README.
Usage: python3 site/build.py <output directory>. Needs npx (marked converts the markdown).
"""
import datetime
import html
import json
import pathlib
import re
import shutil
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SITE_URL = "https://kazalax.github.io/eetkit/"
MARKED = "marked@16.4.2"
HEADING = re.compile(r"<h([1-6])>(.*?)</h\1>", re.DOTALL)
FRONT_MATTER = re.compile(r"\A---\n(.*?)\n---\n", re.DOTALL)

HOME_TITLE = "EetKit: EET 2.0 pro .NET a C#"
HOME_DESCRIPTION = (
    "Open-source knihovna pro C# a .NET, která odesílá tržby do elektronické evidence tržeb EET 2.0 "
    "(rozhraní v4.1): validace, podpis WS-Security, odeslání, ověření odpovědi a POK. "
    "Testováno na .NET 8, .NET 10 a .NET Framework 4.8."
)
HOME_JSONLD = {
    "@context": "https://schema.org",
    "@type": "SoftwareSourceCode",
    "name": "EetKit",
    "description": "C# and .NET library for the Czech electronic sales registration EET 2.0 (interface v4.1).",
    "codeRepository": "https://github.com/Kazalax/eetkit",
    "programmingLanguage": "C#",
    "runtimePlatform": [".NET 8", ".NET 10", ".NET Framework 4.8"],
    "license": "https://opensource.org/licenses/MIT",
    "inLanguage": ["cs", "en"],
    "author": {"@type": "Person", "name": "Bedřich Malý"},
}


def github_slug(text: str) -> str:
    """Anchor the way GitHub makes it, so README links like #výsledek-odeslání keep working."""
    plain = html.unescape(re.sub(r"<[^>]+>", "", text)).strip().lower()
    return re.sub(r"[^\w\- ]", "", plain).replace(" ", "-")


def add_heading_ids(body: str) -> str:
    used: dict[str, int] = {}

    def replace(match: re.Match) -> str:
        slug = github_slug(match.group(2))
        count = used.get(slug, 0)
        used[slug] = count + 1
        anchor = slug if count == 0 else f"{slug}-{count}"
        return f'<h{match.group(1)} id="{anchor}">{match.group(2)}</h{match.group(1)}>'

    return HEADING.sub(replace, body)


def markdown_to_html(markdown: str) -> str:
    return subprocess.run(
        ["npx", "--yes", MARKED, "--gfm"], input=markdown, check=True, capture_output=True, text=True,
    ).stdout


def render(output: pathlib.Path, *, title: str, description: str, url: str, og_type: str, jsonld: dict, body: str) -> None:
    template = (ROOT / "site" / "template.html").read_text(encoding="utf-8")
    values = {
        "title": html.escape(title),
        "description": html.escape(description),
        "url": url,
        "og_type": og_type,
        # "</" would end the script element early.
        "jsonld": json.dumps(jsonld, ensure_ascii=False, indent=2).replace("</", "<\\/"),
        "content": add_heading_ids(body),
    }
    page = re.sub(r"\{\{(\w+)\}\}", lambda m: values[m.group(1)], template)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(page, encoding="utf-8")


def read_front_matter(path: pathlib.Path, required: tuple[str, ...]) -> tuple[dict[str, str], str]:
    text = path.read_text(encoding="utf-8")
    match = FRONT_MATTER.match(text)
    if not match:
        raise ValueError(f"{path} nemá hlavičku ---")
    meta = {}
    for line in match.group(1).splitlines():
        key, _, value = line.partition(":")
        meta[key.strip()] = value.strip()
    for key in required:
        if not meta.get(key):
            raise ValueError(f"{path}: v hlavičce chybí {key}")
    return meta, text[match.end():]


def build_article(path: pathlib.Path, output_dir: pathlib.Path) -> None:
    meta, markdown = read_front_matter(path, ("title", "description", "date", "author"))
    url = f"{SITE_URL}clanky/{path.stem}/"
    published = datetime.date.fromisoformat(meta["date"])
    byline = f'<p class="byline">{html.escape(meta["author"])}, {published.day}. {published.month}. {published.year}</p>'
    body = markdown_to_html(markdown).replace("</h1>", "</h1>\n" + byline, 1)
    jsonld = {
        "@context": "https://schema.org",
        "@type": "TechArticle",
        "headline": meta["title"],
        "description": meta["description"],
        "datePublished": meta["date"],
        "inLanguage": "cs",
        "author": {"@type": "Person", "name": meta["author"]},
        "about": {"@type": "SoftwareSourceCode", "name": "EetKit", "codeRepository": "https://github.com/Kazalax/eetkit"},
        "url": url,
    }
    render(output_dir / "clanky" / path.stem / "index.html", title=meta["title"], description=meta["description"],
           url=url, og_type="article", jsonld=jsonld, body=body)


def write_sitemap(output_dir: pathlib.Path, urls: list[str]) -> None:
    entries = "\n".join(f"  <url><loc>{html.escape(url)}</loc></url>" for url in urls)
    (output_dir / "sitemap.xml").write_text(
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n'
        f"{entries}\n</urlset>\n",
        encoding="utf-8",
    )


def write_indexnow_key(output_dir: pathlib.Path) -> None:
    """IndexNow (Bing and others) checks site ownership by fetching <key>.txt next to the pages."""
    key = (ROOT / "site" / "indexnow-key.txt").read_text(encoding="utf-8").strip()
    (output_dir / f"{key}.txt").write_text(key, encoding="utf-8")


def build_page(path: pathlib.Path, output_dir: pathlib.Path) -> str:
    """A standalone page from site/stranky, served at /<file name>/ (for example /pro/)."""
    meta, markdown = read_front_matter(path, ("title", "description"))
    url = f"{SITE_URL}{path.stem}/"
    jsonld = {
        "@context": "https://schema.org",
        "@type": "WebPage",
        "name": meta["title"],
        "description": meta["description"],
        "inLanguage": "cs",
        "url": url,
    }
    render(output_dir / path.stem / "index.html", title=meta["title"], description=meta["description"],
           url=url, og_type="website", jsonld=jsonld, body=markdown_to_html(markdown))
    return url


def main() -> int:
    output_dir = pathlib.Path(sys.argv[1])
    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    render(output_dir / "index.html", title=HOME_TITLE, description=HOME_DESCRIPTION, url=SITE_URL,
           og_type="website", jsonld=HOME_JSONLD, body=markdown_to_html(readme))
    urls = [SITE_URL]
    for article in sorted((ROOT / "site" / "clanky").glob("*.md")):
        build_article(article, output_dir)
        urls.append(f"{SITE_URL}clanky/{article.stem}/")
    for page in sorted((ROOT / "site" / "stranky").glob("*.md")):
        urls.append(build_page(page, output_dir))
    write_sitemap(output_dir, urls)
    write_indexnow_key(output_dir)
    shutil.copyfile(ROOT / "llms.txt", output_dir / "llms.txt")
    # Google Search Console ownership file, served unchanged next to the pages.
    for verification in (ROOT / "site").glob("google*.html"):
        shutil.copyfile(verification, output_dir / verification.name)
    return 0


if __name__ == "__main__":
    sys.exit(main())
