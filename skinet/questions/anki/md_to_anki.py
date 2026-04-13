#!/usr/bin/env python3
"""
md_to_anki.py — Converts Q&A markdown to Anki-importable TSV.

Usage:
    py md_to_anki.py <input.md> [output.txt] [--section N]

Section auto-detection from filename:
    section-a-... → S01,  section-b-... → S02,  section-c-... → S03, etc.

Output card format:
    Front: S02.01.Easy. What is middleware in ASP.NET Core?
    Back:  HTML-formatted answer (bullet points, bold, inline code)
"""

import re
import sys
from pathlib import Path

SECTION_LETTER_MAP = {chr(ord('a') + i): i + 1 for i in range(26)}


def detect_section_number(filepath: Path) -> int:
    name = filepath.stem.lower()
    match = re.search(r'section[-_]([a-z])', name)
    if match:
        return SECTION_LETTER_MAP.get(match.group(1), 1)
    return 1


def detect_deck_name(filepath: Path) -> str:
    """Build Anki deck path: E-Commerce - Sprints::Sprint N::Section X"""
    # Sprint from parent folder: sprint-1-interview → Sprint 1
    parent = filepath.parent.name.lower()
    sprint_match = re.search(r'sprint[-_](\d+)', parent)
    sprint_label = f"Sprint {sprint_match.group(1)}" if sprint_match else "Sprint 1"

    # Section from filename: section-a-... → Section A
    stem = filepath.stem.lower()
    section_match = re.search(r'section[-_]([a-z])', stem)
    section_label = f"Section {section_match.group(1).upper()}" if section_match else "Section A"

    return f"E-Commerce - Sprints::{sprint_label}::{section_label}"


def md_inline_to_html(text: str) -> str:
    text = re.sub(r'`([^`]+)`', r'<code>\1</code>', text)
    text = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', text)
    text = re.sub(r'(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)', r'<i>\1</i>', text)
    return text


def md_block_to_html(text: str) -> str:
    text = text.strip()
    lines = text.split('\n')
    html_parts = []
    in_list = False
    in_code_block = False
    code_lines = []

    for line in lines:
        if line.strip().startswith('```'):
            if in_code_block:
                code_content = '\n'.join(code_lines)
                code_content = code_content.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')
                html_parts.append(f'<pre><code>{code_content}</code></pre>')
                code_lines = []
                in_code_block = False
            else:
                if in_list:
                    html_parts.append('</ul>')
                    in_list = False
                in_code_block = True
            continue

        if in_code_block:
            code_lines.append(line)
            continue

        stripped = line.strip()

        if not stripped:
            if in_list:
                html_parts.append('</ul>')
                in_list = False
            continue

        if re.match(r'^[-*]\s+', stripped):
            if not in_list:
                html_parts.append('<ul>')
                in_list = True
            content = re.sub(r'^[-*]\s+', '', stripped)
            html_parts.append(f'<li>{md_inline_to_html(content)}</li>')
        elif re.match(r'^\d+\.\s+', stripped):
            if in_list:
                html_parts.append('</ul>')
                in_list = False
            content = re.sub(r'^\d+\.\s+', '', stripped)
            html_parts.append(f'<li>{md_inline_to_html(content)}</li>')
        else:
            if in_list:
                html_parts.append('</ul>')
                in_list = False
            html_parts.append(f'<p>{md_inline_to_html(stripped)}</p>')

    if in_list:
        html_parts.append('</ul>')

    return ''.join(html_parts)


def make_front_prefix(section_num: int, card_num: int, difficulty: str) -> str:
    return f"S{section_num:02d}.{card_num:02d}.{difficulty.capitalize()}."


def parse_md_to_cards(md_text: str, section_num: int) -> list:
    cards = []
    current_difficulty = "Easy"
    card_counter = 0

    difficulty_sections = re.split(r'(### (?:EASY|MEDIUM|HARD))', md_text)

    for chunk in difficulty_sections:
        chunk_stripped = chunk.strip()

        if chunk_stripped in ('### EASY', '### MEDIUM', '### HARD'):
            current_difficulty = chunk_stripped.replace('### ', '').capitalize()
            continue

        question_blocks = re.split(r'(?=\*\*[A-Z]\d+(?:\.\d+)*\.\*\*)', chunk)

        for block in question_blocks:
            block = block.strip()
            if not block:
                continue
            if not re.match(r'\*\*[A-Z]\d+(?:\.\d+)*\.\*\*', block):
                continue

            q_match = re.match(
                r'\*\*[A-Z]\d+(?:\.\d+)*\.\*\*\s*(.*?)(?=<details|\Z)',
                block, re.DOTALL
            )
            if not q_match:
                continue
            question_raw = q_match.group(1).strip()

            answer_match = re.search(
                r'<details><summary>Answer</summary>(.*?)</details>',
                block, re.DOTALL
            )
            if not answer_match:
                continue
            answer_raw = answer_match.group(1).strip()

            card_counter += 1
            prefix = make_front_prefix(section_num, card_counter, current_difficulty)
            front = f"{prefix} {md_inline_to_html(question_raw)}"
            back = md_block_to_html(answer_raw)
            cards.append((front, back))

    return cards


def write_tsv(cards: list, output_path: Path, deck_name: str = "Imported from MD") -> None:
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write("#separator:tab\n")
        f.write("#html:true\n")
        f.write("#notetype:Basic\n")
        f.write(f"#deck:{deck_name}\n")
        for front, back in cards:
            front_clean = front.replace('\t', ' ').replace('\n', ' ')
            back_clean = back.replace('\t', ' ').replace('\n', ' ')
            f.write(f"{front_clean}\t{back_clean}\n")


def main():
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
    deck_name = detect_deck_name(input_path)
    md_text = input_path.read_text(encoding='utf-8')
    cards = parse_md_to_cards(md_text, section_num)

    if not cards:
        print("No cards found. Check your markdown format.")
        sys.exit(1)

    write_tsv(cards, output_path, deck_name)
    print(f"Done! {len(cards)} cards -> {output_path}")
    print(f"   Deck: {deck_name} | Section: S{section_num:02d}")


if __name__ == "__main__":
    main()
