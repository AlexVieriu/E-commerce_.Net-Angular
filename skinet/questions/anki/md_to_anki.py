#!/usr/bin/env python3
"""
md_to_anki.py — Converts Q&A markdown to Anki-importable TSV.

Usage:
    py md_to_anki.py <input.md> [output.txt] [--section N]

Section auto-detection from filename:
    section-a-... → S01,  section-b-... → S02,  section-c-... → S03, etc.

Output files (both written on every run):
    <stem>_anki.txt    — bulk-importable TSV; # comment line above each card
    <stem>_answers.txt — back-field HTML only, one labeled block per card
                         (open this when updating a single card in Anki)

Output card format:
    Front: [EASY badge] S01.01  Question text (inline code styled)
    Back:  <style> block + scoped .ac wrapper + class-based HTML
"""

import re
import sys
from dataclasses import dataclass
from pathlib import Path

SECTION_LETTER_MAP = {chr(ord('a') + i): i + 1 for i in range(26)}

# ── One Dark Pro palette ──────────────────────────────────────────────────────
# Background: #282c34   Surface:  #3e4451   Muted:  #5c6370
# Text:       #abb2bf   Cyan:     #56b6c2   Blue:   #61afef
# Green:      #98c379   Orange:   #d19a66   Red:    #e06c75

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

BACK_STYLE_BLOCK = (
    "<style>"
    ".c{background:#3e4451;color:#56b6c2;"
    "padding:1px 6px;border-radius:4px;"
    "font-family:monospace;font-size:.88em}"
    ".p{background:#282c34;color:#abb2bf;"
    "padding:12px 16px;border-radius:6px;"
    "border-left:3px solid #61afef;"
    "font-family:monospace;font-size:13px;"
    "white-space:pre-wrap;word-break:break-word;"
    "margin:10px 0;display:block}"
    ".ac{font-family:system-ui,-apple-system,sans-serif;"
    "line-height:1.65;font-size:0.95em;text-align:left}"
    ".ac p{margin:.4em 0}"
    ".ac ul,.ac ol{margin:.4em 0;padding-left:1.6em}"
    ".ac li{margin:.35em 0}"
    ".ac li>p{margin:0}"
    ".ac b{color:#56b6c2}"
    ".ac li::marker{color:#61afef}"
    ".rf{border-left:3px solid #e06c75;background:#2a1f22;"
    "padding:10px 14px;border-radius:0 6px 6px 0;margin:14px 0}"
    ".rf-label{color:#e06c75;font-family:monospace;font-size:0.72em;"
    "font-weight:700;letter-spacing:0.08em;text-transform:uppercase;margin:0 0 6px}"
    ".gf{border-left:3px solid #98c379;background:#1b2b1e;"
    "padding:10px 14px;border-radius:0 6px 6px 0;margin:14px 0}"
    ".gf-label{color:#98c379;font-family:monospace;font-size:0.72em;"
    "font-weight:700;letter-spacing:0.08em;text-transform:uppercase;margin:0 0 6px}"
    ".af{border-left:3px solid #61afef;background:#1e2430;"
    "padding:10px 14px;border-radius:0 6px 6px 0;margin:0 0 14px}"
    ".af-label{color:#61afef;font-family:monospace;font-size:0.72em;"
    "font-weight:700;letter-spacing:0.08em;text-transform:uppercase;margin:0 0 6px}"
    "</style>"
)


# ── Card dataclass ────────────────────────────────────────────────────────────

@dataclass
class Card:
    card_num: int
    difficulty: str
    freq: str | None
    label: str      # comment line, e.g. "# A10 — [HARD][HIGH] The CI suite..."
    front: str
    back: str


# ── Helpers ───────────────────────────────────────────────────────────────────

def detect_section_number(filepath: Path) -> int:
    match = re.search(r'section[-_]([a-z])', filepath.stem.lower())
    return SECTION_LETTER_MAP.get(match.group(1), 1) if match else 1


def detect_deck_name(filepath: Path) -> str:
    sprint_match = re.search(r'sprint[-_](\d+)', filepath.parent.name.lower())
    sprint_label = f"Sprint {sprint_match.group(1)}" if sprint_match else "Sprint 1"
    section_match = re.search(r'section[-_]([a-z])', filepath.stem.lower())
    section_label = f"Section {section_match.group(1).upper()}" if section_match else "Section A"
    return f"E-Commerce - Sprints::{sprint_label}::{section_label}"


def escape_html(text: str) -> str:
    return text.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')


def _make_label(section_letter: str, card_num: int, difficulty: str, freq: str | None, question_raw: str) -> str:
    """Build the # comment line shown above each card in both output files."""
    first_line = question_raw.split('\n')[0]
    first_line = re.sub(r'\*+([^*]+)\*+', r'\1', first_line)    # strip bold/italic
    first_line = re.sub(r'`([^`]+)`', r'\1', first_line)        # strip inline code
    first_line = re.sub(r'\[OUT-OF-SCOPE\]\s*', '', first_line) # strip tag
    first_line = first_line.strip()
    if len(first_line) > 72:
        first_line = first_line[:69] + '...'
    diff_tag = f'[{difficulty.upper()}]'
    freq_tag = f'[{freq}]' if freq else ''
    return f'# {section_letter}{card_num} — {diff_tag}{freq_tag} {first_line}'


def md_inline_to_html(text: str, *, for_back: bool = False) -> str:
    code_tag = 'class="c"' if for_back else f'style="{FRONT_CODE_STYLE}"'
    pre_tag  = 'class="p"' if for_back else f'style="{FRONT_PRE_STYLE}"'

    def replace_fenced(m: re.Match) -> str:
        code = escape_html(m.group(1).lstrip('\n'))
        code = code.replace('\n', '<br>')
        return f'<pre {pre_tag}><code>{code}</code></pre>'

    text = re.sub(r'```(?:\w+)?\n?(.*?)```', replace_fenced, text, flags=re.DOTALL)
    text = re.sub(
        r'`([^`\n]+)`',
        lambda m: f'<code {code_tag}>{escape_html(m.group(1))}</code>',
        text,
    )
    text = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', text)
    text = re.sub(r'(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)', r'<i>\1</i>', text)
    text = re.sub(r'(?m)^\s*https?://\S+\s*$', '', text)
    return text.strip()


def md_block_to_html(text: str) -> str:
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

        if re.match(r'^[-*]\s+', stripped):
            if in_ol:
                close_list()
            if not in_ul:
                html_parts.append('<ul>')
                in_ul = True
            content = re.sub(r'^[-*]\s+', '', stripped)
            html_parts.append(f'<li>{md_inline_to_html(content, for_back=True)}</li>')

        elif re.match(r'^\d+\.\s+', stripped):
            if in_ul:
                close_list()
            if not in_ol:
                html_parts.append('<ol>')
                in_ol = True
            content = re.sub(r'^\d+\.\s+', '', stripped)
            html_parts.append(f'<li>{md_inline_to_html(content, for_back=True)}</li>')

        else:
            close_list()
            html_parts.append(f'<p>{md_inline_to_html(stripped, for_back=True)}</p>')

    close_list()
    return ''.join(html_parts)


def make_front(section_num: int, card_num: int, difficulty: str, question_html: str, freq: str | None = None) -> str:
    badge = DIFFICULTY_BADGE.get(difficulty, difficulty)
    freq_badge = f' · {FREQ_BADGE[freq]}' if freq and freq in FREQ_BADGE else ''
    meta = (
        f'<div style="font-family:monospace;font-size:0.68em;color:#5c6370;'
        f'margin-bottom:10px;letter-spacing:0.03em">'
        f'S{section_num:02d}.{card_num:02d} · {badge}{freq_badge}'
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
    lclass = 'rf-label' if kind == 'rf' else 'gf-label'
    return (
        f'<div class="{kind}">'
        f'<p class="{lclass}">{label}</p>'
        f'<div class="ac">{md_block_to_html(raw)}</div>'
        f'</div>'
    )


# ── Parser ────────────────────────────────────────────────────────────────────

def parse_md_to_cards(md_text: str, section_num: int) -> list[Card]:
    section_letter = chr(ord('A') + section_num - 1)
    cards: list[Card] = []
    current_difficulty = 'Easy'
    card_counter = 0

    _Q_ID = r'[A-Z](?:-[A-Z]+)?\d+(?:\.\d+)*'

    for chunk in re.split(r'(### (?:EASY|MEDIUM|HARD|BEHAVIORAL)|##[^#]*Syntax Quick-Fire)', md_text):
        chunk_stripped = chunk.strip()

        if re.match(r'^### (?:EASY|MEDIUM|HARD|BEHAVIORAL)$', chunk_stripped):
            label_text = chunk_stripped.replace('### ', '').capitalize()
            current_difficulty = 'Medium' if label_text == 'Behavioral' else label_text
            continue

        if re.search(r'Syntax Quick-Fire', chunk_stripped):
            current_difficulty = 'Easy'
            continue

        for block in re.split(rf'(?=\*\*{_Q_ID}\.\*\*)', chunk):
            block = block.strip()
            if not block or not re.match(rf'\*\*{_Q_ID}\.\*\*', block):
                continue

            q_match = re.match(
                rf'\*\*{_Q_ID}\.\*\*\s*(.*?)(?=<details|\Z)',
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
            question_raw = q_match.group(1).strip()

            freq_match = re.match(r'^\[(HIGH|MEDIUM|LOW)\]\s*', question_raw)
            freq = freq_match.group(1) if freq_match else None
            if freq_match:
                question_raw = question_raw[freq_match.end():]

            question_html = md_inline_to_html(question_raw, for_back=False)
            front = make_front(section_num, card_counter, current_difficulty, question_html, freq)
            back  = BACK_STYLE_BLOCK + back_content
            label = _make_label(section_letter, card_counter, current_difficulty, freq, question_raw)

            cards.append(Card(
                card_num=card_counter,
                difficulty=current_difficulty,
                freq=freq,
                label=label,
                front=front,
                back=back,
            ))

    return cards


# ── Writers ───────────────────────────────────────────────────────────────────

def write_tsv(cards: list[Card], output_path: Path, deck_name: str) -> None:
    """Bulk-importable TSV. Each card is preceded by a # comment line."""
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write('#separator:tab\n')
        f.write('#html:true\n')
        f.write('#notetype:Basic\n')
        f.write(f'#deck:{deck_name}\n')
        for card in cards:
            f.write(f'{card.label}\n')
            front_clean = card.front.replace('\t', ' ').replace('\n', ' ')
            back_clean  = card.back.replace('\t', ' ').replace('\n', ' ')
            f.write(f'{front_clean}\t{back_clean}\n')


def write_answers(cards: list[Card], output_path: Path, deck_name: str, source_name: str) -> None:
    """
    Companion file — back-field HTML only, one labeled block per card.
    Open this file when you need to update a single card in Anki:
      1. Find the card label (e.g. # A10)
      2. Copy the HTML block below the divider
      3. Paste into the Back field in the Anki card editor
    """
    divider = '# ' + '─' * 76
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(f'# Answers companion — {source_name}\n')
        f.write(f'# Deck: {deck_name}\n')
        f.write('#\n')
        f.write('# Usage: find the card label, copy the HTML block below the divider,\n')
        f.write('# paste into the Back field of that card in the Anki card editor.\n')
        f.write('\n')
        for card in cards:
            f.write(f'{divider}\n')
            f.write(f'{card.label}\n')
            f.write(f'{divider}\n')
            back_clean = card.back.replace('\t', ' ').replace('\n', ' ')
            f.write(f'{back_clean}\n')
            f.write('\n')


# ── Entry point ───────────────────────────────────────────────────────────────

def main() -> None:
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        sys.exit(0)

    input_path = Path(args[0])
    if not input_path.exists():
        print(f'Error: File not found: {input_path}')
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
        anki_dir = input_path.parent / 'anki'
        anki_dir.mkdir(exist_ok=True)
        output_path = anki_dir / (input_path.stem + '_anki.txt')

    answers_path = output_path.with_name(
        output_path.name.replace('_anki.txt', '_answers.txt')
    )

    section_num = section_override if section_override else detect_section_number(input_path)
    deck_name   = detect_deck_name(input_path)
    md_text     = input_path.read_text(encoding='utf-8')
    cards       = parse_md_to_cards(md_text, section_num)

    if not cards:
        print('No cards found. Check your markdown format.')
        sys.exit(1)

    write_tsv(cards, output_path, deck_name)
    write_answers(cards, answers_path, deck_name, input_path.name)

    print(f'Done! {len(cards)} cards')
    print(f'  Import:  {output_path}')
    print(f'  Answers: {answers_path}')
    print(f'  Deck: {deck_name} | Section: S{section_num:02d}')


if __name__ == '__main__':
    main()
