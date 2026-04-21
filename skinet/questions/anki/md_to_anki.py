#!/usr/bin/env python3
"""
md_to_anki.py — Converts Q&A markdown to Anki-importable TSV.

Usage:
    py md_to_anki.py <input.md> [output.txt] [--section N]

Section auto-detection from filename:
    section-a-... → S01,  section-b-... → S02,  section-c-... → S03, etc.

Output card format:
    Front: [EASY badge] S01.01  Question text (inline code styled)
    Back:  <style> block + scoped .ac wrapper + class-based HTML
"""

import re
import sys
from pathlib import Path

SECTION_LETTER_MAP = {chr(ord('a') + i): i + 1 for i in range(26)}

# ── One Dark Pro palette ──────────────────────────────────────────────────────
# Background: #282c34   Surface:  #3e4451   Muted:  #5c6370
# Text:       #abb2bf   Cyan:     #56b6c2   Blue:   #61afef
# Green:      #98c379   Orange:   #d19a66   Red:    #e06c75

# ── Difficulty badges (front field — must use inline styles, no sheet active) ─
# ── Interview frequency badges ───────────────────────────────────────────────
FREQ_BADGE: dict[str, str] = {
    "HIGH": (
        '<span style="background:#4078f2;color:#fff;'
        'padding:2px 9px;border-radius:12px;'
        'font-size:0.7em;font-weight:700;font-family:sans-serif;'
        'vertical-align:middle;letter-spacing:0.06em;text-transform:uppercase">'
        '★ HIGH</span>'
    ),
    "MEDIUM": (
        '<span style="background:#986801;color:#fff;'
        'padding:2px 9px;border-radius:12px;'
        'font-size:0.7em;font-weight:700;font-family:sans-serif;'
        'vertical-align:middle;letter-spacing:0.06em;text-transform:uppercase">'
        '◆ MED</span>'
    ),
    "LOW": (
        '<span style="background:#5c6370;color:#fff;'
        'padding:2px 9px;border-radius:12px;'
        'font-size:0.7em;font-weight:700;font-family:sans-serif;'
        'vertical-align:middle;letter-spacing:0.06em;text-transform:uppercase">'
        '○ LOW</span>'
    ),
}

# ── Difficulty badges (front field — must use inline styles, no sheet active) ─
DIFFICULTY_BADGE: dict[str, str] = {
    "Easy": (
        '<span style="background:#98c379;color:#282c34;'
        'padding:2px 9px;border-radius:12px;'
        'font-size:0.7em;font-weight:700;font-family:sans-serif;'
        'vertical-align:middle;letter-spacing:0.06em;text-transform:uppercase">'
        'Easy</span>'
    ),
    "Medium": (
        '<span style="background:#d19a66;color:#282c34;'
        'padding:2px 9px;border-radius:12px;'
        'font-size:0.7em;font-weight:700;font-family:sans-serif;'
        'vertical-align:middle;letter-spacing:0.06em;text-transform:uppercase">'
        'Medium</span>'
    ),
    "Hard": (
        '<span style="background:#e06c75;color:#282c34;'
        'padding:2px 9px;border-radius:12px;'
        'font-size:0.7em;font-weight:700;font-family:sans-serif;'
        'vertical-align:middle;letter-spacing:0.06em;text-transform:uppercase">'
        'Hard</span>'
    ),
}

# ── Front field: inline code styles (stylesheet not active on question view) ──
FRONT_CODE_STYLE = (
    "background:#3e4451;color:#56b6c2;"
    "padding:1px 6px;border-radius:4px;"
    "font-family:monospace;font-size:0.88em"
)
FRONT_PRE_STYLE = (
    "background:#282c34;color:#abb2bf;"
    "padding:12px 16px;border-radius:6px;"
    "border-left:3px solid #61afef;"
    "font-family:monospace;font-size:13px;"
    "white-space:pre-wrap;word-break:break-word;"
    "margin:10px 0;display:block"
)

# ── Back field: one stylesheet per card + short class names ──────────────────
# Anki renders the answer page as:  {{FrontSide}} <hr> {{Back}}
# A <style> in Back applies to the whole page — including the re-rendered front.
# On the question-only page only {{Front}} renders, so front still needs inline styles.
BACK_STYLE_BLOCK = (
    "<style>"
    # Inline code
    ".c{background:#3e4451;color:#56b6c2;"
    "padding:1px 6px;border-radius:4px;"
    "font-family:monospace;font-size:.88em}"
    # Code block
    ".p{background:#282c34;color:#abb2bf;"
    "padding:12px 16px;border-radius:6px;"
    "border-left:3px solid #61afef;"
    "font-family:monospace;font-size:13px;"
    "white-space:pre-wrap;word-break:break-word;"
    "margin:10px 0;display:block}"
    # Answer wrapper
    ".ac{font-family:system-ui,-apple-system,sans-serif;"
    "line-height:1.65;font-size:0.95em;text-align:left}"
    ".ac p{margin:.4em 0}"
    ".ac ul,.ac ol{margin:.4em 0;padding-left:1.6em}"
    ".ac li{margin:.35em 0}"
    ".ac li>p{margin:0}"
    ".ac b{color:#56b6c2}"
    ".ac li::marker{color:#61afef}"
    # Red / Green flag callout boxes
    ".rf{border-left:3px solid #e06c75;background:#2a1f22;"
    "padding:10px 14px;border-radius:0 6px 6px 0;margin:14px 0}"
    ".rf-label{color:#e06c75;font-family:monospace;font-size:0.72em;"
    "font-weight:700;letter-spacing:0.08em;text-transform:uppercase;margin:0 0 6px}"
    ".gf{border-left:3px solid #98c379;background:#1b2b1e;"
    "padding:10px 14px;border-radius:0 6px 6px 0;margin:14px 0}"
    ".gf-label{color:#98c379;font-family:monospace;font-size:0.72em;"
    "font-weight:700;letter-spacing:0.08em;text-transform:uppercase;margin:0 0 6px}"
    # Answer box
    ".af{border-left:3px solid #61afef;background:#1e2430;"
    "padding:10px 14px;border-radius:0 6px 6px 0;margin:0 0 14px}"
    ".af-label{color:#61afef;font-family:monospace;font-size:0.72em;"
    "font-weight:700;letter-spacing:0.08em;text-transform:uppercase;margin:0 0 6px}"
    "</style>"
)


def detect_section_number(filepath: Path) -> int:
    match = re.search(r'section[-_]([a-z])', filepath.stem.lower())
    return SECTION_LETTER_MAP.get(match.group(1), 1) if match else 1


def detect_deck_name(filepath: Path) -> str:
    """Build Anki deck path: E-Commerce - Sprints::Sprint N::Section X"""
    sprint_match = re.search(r'sprint[-_](\d+)', filepath.parent.name.lower())
    sprint_label = f"Sprint {sprint_match.group(1)}" if sprint_match else "Sprint 1"

    section_match = re.search(r'section[-_]([a-z])', filepath.stem.lower())
    section_label = f"Section {section_match.group(1).upper()}" if section_match else "Section A"

    return f"E-Commerce - Sprints::{sprint_label}::{section_label}"


def escape_html(text: str) -> str:
    return text.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')


def md_inline_to_html(text: str, *, for_back: bool = False) -> str:
    """
    Convert inline markdown to HTML.
    for_back=True  → class="c" / class="p"  (back field, stylesheet active)
    for_back=False → full inline style       (front field, no stylesheet)
    Also handles fenced code blocks inside question text.
    """
    code_tag = 'class="c"' if for_back else f'style="{FRONT_CODE_STYLE}"'
    pre_tag  = 'class="p"' if for_back else f'style="{FRONT_PRE_STYLE}"'

    # 1. Fenced code blocks — handle before single-backtick to avoid partial matches
    def replace_fenced(m: re.Match) -> str:
        code = escape_html(m.group(1).lstrip('\n'))
        code = code.replace('\n', '<br>')
        return f'<pre {pre_tag}><code>{code}</code></pre>'

    text = re.sub(r'```(?:\w+)?\n?(.*?)```', replace_fenced, text, flags=re.DOTALL)

    # 2. Inline code — escape content so IEnumerable<T> doesn't become an HTML tag
    text = re.sub(
        r'`([^`\n]+)`',
        lambda m: f'<code {code_tag}>{escape_html(m.group(1))}</code>',
        text,
    )

    # 3. Bold then italic (order matters — bold must run first)
    text = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', text)
    text = re.sub(r'(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)', r'<i>\1</i>', text)

    # 4. Strip bare URL lines (claude.ai session links, etc.)
    text = re.sub(r'(?m)^\s*https?://\S+\s*$', '', text)

    return text.strip()


def md_block_to_html(text: str) -> str:
    """Convert a full markdown answer block to class-based HTML (for back field)."""
    text = re.sub(r'(?m)^\s*https?://\S+\s*$', '', text)
    text = text.strip()

    lines = text.split('\n')
    html_parts: list[str] = []
    in_ul = in_ol = in_code_block = False
    code_lines: list[str] = []

    def close_list() -> None:
        nonlocal in_ul, in_ol
        if in_ul:
            html_parts.append('</ul>')
            in_ul = False
        if in_ol:
            html_parts.append('</ol>')
            in_ol = False

    for line in lines:
        # Fenced code block delimiter
        if line.strip().startswith('```'):
            if in_code_block:
                code_content = escape_html('\n'.join(code_lines)).replace('\n', '<br>')
                html_parts.append(f'<pre class="p"><code>{code_content}</code></pre>')
                code_lines = []
                in_code_block = False
            else:
                close_list()
                in_code_block = True
            continue

        if in_code_block:
            code_lines.append(line)
            continue

        stripped = line.strip()

        if not stripped:
            close_list()
            continue

        # Unordered list
        if re.match(r'^[-*]\s+', stripped):
            if in_ol:
                close_list()
            if not in_ul:
                html_parts.append('<ul>')
                in_ul = True
            content = re.sub(r'^[-*]\s+', '', stripped)
            html_parts.append(f'<li>{md_inline_to_html(content, for_back=True)}</li>')

        # Ordered list
        elif re.match(r'^\d+\.\s+', stripped):
            if in_ul:
                close_list()
            if not in_ol:
                html_parts.append('<ol>')
                in_ol = True
            content = re.sub(r'^\d+\.\s+', '', stripped)
            html_parts.append(f'<li>{md_inline_to_html(content, for_back=True)}</li>')

        # Regular paragraph
        else:
            close_list()
            html_parts.append(f'<p>{md_inline_to_html(stripped, for_back=True)}</p>')

    close_list()
    return ''.join(html_parts)


def make_front(section_num: int, card_num: int, difficulty: str, question_html: str, freq: str | None = None) -> str:
    """
    Two-row front layout:
      Row 1 — small metadata: card ID · difficulty badge · frequency badge
      Row 2 — larger question text (the hero element on a flashcard)
    """
    badge = DIFFICULTY_BADGE.get(difficulty, difficulty)
    freq_badge = f'\u2002\u00b7\u2002{FREQ_BADGE[freq]}' if freq and freq in FREQ_BADGE else ''
    meta = (
        f'<div style="font-family:monospace;font-size:0.68em;color:#5c6370;'
        f'margin-bottom:10px;letter-spacing:0.03em">'
        f'S{section_num:02d}.{card_num:02d}\u2002\u00b7\u2002{badge}{freq_badge}'
        f'</div>'
    )
    question = (
        f'<div style="font-family:system-ui,-apple-system,sans-serif;'
        f'font-size:1.08em;line-height:1.55">'
        f'{question_html}'
        f'</div>'
    )
    return f'<div style="text-align:left">{meta}{question}</div>'


DETAIL_ANSWER_RE = re.compile(
    r'<details>\s*<summary>The 5% Answer[^<]*</summary>(.*?)</details>',
    re.DOTALL,
)
DETAIL_REDFLAG_RE = re.compile(
    r'<details>\s*<summary>Red Flag</summary>(.*?)</details>',
    re.DOTALL,
)
DETAIL_GREENFLAG_RE = re.compile(
    r'<details>\s*<summary>Green Flag</summary>(.*?)</details>',
    re.DOTALL,
)

def _flag_section(kind: str, label: str, raw: str) -> str:
    """kind: 'rf' (red flag) or 'gf' (green flag)"""
    lclass = 'rf-label' if kind == 'rf' else 'gf-label'
    return (
        f'<div class="{kind}">'
        f'<p class="{lclass}">{label}</p>'
        f'<div class="ac">{md_block_to_html(raw)}</div>'
        f'</div>'
    )


def parse_md_to_cards(md_text: str, section_num: int) -> list[tuple[str, str]]:
    cards: list[tuple[str, str]] = []
    current_difficulty = "Easy"
    card_counter = 0

    for chunk in re.split(r'(### (?:EASY|MEDIUM|HARD|BEHAVIORAL))', md_text):
        chunk_stripped = chunk.strip()

        if re.match(r'^### (?:EASY|MEDIUM|HARD|BEHAVIORAL)$', chunk_stripped):
            label = chunk_stripped.replace('### ', '').capitalize()
            current_difficulty = 'Medium' if label == 'Behavioral' else label
            continue

        for block in re.split(r'(?=\*\*[A-Z]\d+(?:\.\d+)*\.\*\*)', chunk):
            block = block.strip()
            if not block or not re.match(r'\*\*[A-Z]\d+(?:\.\d+)*\.\*\*', block):
                continue

            q_match = re.match(
                r'\*\*[A-Z]\d+(?:\.\d+)*\.\*\*\s*(.*?)(?=<details|\Z)',
                block, re.DOTALL,
            )
            if not q_match:
                continue

            answer_m = DETAIL_ANSWER_RE.search(block)
            if not answer_m:
                continue

            answer_raw = re.sub(
                r'^\*\*The 5% Answer[^*]*\*\*\s*\n?', '',
                answer_m.group(1).strip(),
            ).strip()

            back_content = (
                f'<div class="af">'
                f'<p class="af-label">★ The 5% Answer</p>'
                f'<div class="ac">{md_block_to_html(answer_raw)}</div>'
                f'</div>'
            )

            red_m = DETAIL_REDFLAG_RE.search(block)
            if red_m:
                red_raw = re.sub(r'^\*\*Red Flag:\*\*\s*\n?', '', red_m.group(1).strip()).strip()
                back_content += _flag_section('rf', '🚩 Red Flag — shallow answer', red_raw)

            green_m = DETAIL_GREENFLAG_RE.search(block)
            if green_m:
                green_raw = re.sub(r'^\*\*Green Flag:\*\*\s*\n?', '', green_m.group(1).strip()).strip()
                back_content += _flag_section('gf', '✓ Green Flag — pro insight', green_raw)

            card_counter += 1
            question_raw  = q_match.group(1).strip()
            # Extract optional frequency tag [HIGH/MEDIUM/LOW] at start of question
            freq_match = re.match(r'^\[(HIGH|MEDIUM|LOW)\]\s*', question_raw)
            freq = freq_match.group(1) if freq_match else None
            if freq_match:
                question_raw = question_raw[freq_match.end():]
            question_html = md_inline_to_html(question_raw, for_back=False)
            front = make_front(section_num, card_counter, current_difficulty, question_html, freq)
            back  = BACK_STYLE_BLOCK + back_content

            cards.append((front, back))

    return cards


def write_tsv(
    cards: list[tuple[str, str]],
    output_path: Path,
    deck_name: str = "Imported from MD",
) -> None:
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write("#separator:tab\n")
        f.write("#html:true\n")
        f.write("#notetype:Basic\n")
        f.write(f"#deck:{deck_name}\n")
        for front, back in cards:
            # Remaining structural \n → space (code blocks already use <br>)
            front_clean = front.replace('\t', ' ').replace('\n', ' ')
            back_clean  = back.replace('\t', ' ').replace('\n', ' ')
            f.write(f"{front_clean}\t{back_clean}\n")


def main() -> None:
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        sys.exit(0)

    input_path = Path(args[0])
    if not input_path.exists():
        print(f"Error: File not found: {input_path}")
        sys.exit(1)

    output_path = None
    section_override = None
    i = 1
    while i < len(args):
        if args[i] == '--section' and i + 1 < len(args):
            section_override = int(args[i + 1])
            i += 2
        elif not output_path:
            output_path = Path(args[i])
            i += 1
        else:
            i += 1

    if not output_path:
        anki_dir = input_path.parent / "anki"
        anki_dir.mkdir(exist_ok=True)
        output_path = anki_dir / (input_path.stem + "_anki.txt")

    section_num = section_override if section_override else detect_section_number(input_path)
    deck_name   = detect_deck_name(input_path)
    md_text     = input_path.read_text(encoding='utf-8')
    cards       = parse_md_to_cards(md_text, section_num)

    if not cards:
        print("No cards found. Check your markdown format.")
        sys.exit(1)

    write_tsv(cards, output_path, deck_name)
    print(f"Done! {len(cards)} cards -> {output_path}")
    print(f"   Deck: {deck_name} | Section: S{section_num:02d}")


if __name__ == "__main__":
    main()
