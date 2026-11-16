#!/usr/bin/env python3
"""Builds the GitHub Pages site from README.md: one page, so the site never says more than the README.

Usage: python3 site/build.py <output directory>. Needs npx (marked converts the markdown).
"""
import html
import pathlib
import re
import shutil
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
MARKED = "marked@16.4.2"
HEADING = re.compile(r"<h([1-6])>(.*?)</h\1>", re.DOTALL)


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


def main() -> int:
    output = pathlib.Path(sys.argv[1])
    output.mkdir(parents=True, exist_ok=True)
    body = subprocess.run(
        ["npx", "--yes", MARKED, "--gfm", "-i", str(ROOT / "README.md")],
        check=True, capture_output=True, text=True,
    ).stdout
    page = (ROOT / "site" / "template.html").read_text(encoding="utf-8").replace("{{content}}", add_heading_ids(body))
    (output / "index.html").write_text(page, encoding="utf-8")
    shutil.copyfile(ROOT / "llms.txt", output / "llms.txt")
    return 0


if __name__ == "__main__":
    sys.exit(main())
