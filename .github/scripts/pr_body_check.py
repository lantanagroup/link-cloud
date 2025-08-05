import os
import re

def warn(msg: str) -> None:
    print(f"::warning::{msg}")

def normalize(s: str) -> str:
    return s.replace('\r\n', '\n').replace('\r', '\n').strip()

def is_na(text: str) -> bool:
    # Accepts n/a, na, N/A, NA, n.a., N.A., etc.
    return bool(re.fullmatch(r'\s*(n[\./]?\s*a[\./]?|not\s*applicable)\s*', text.strip(), re.IGNORECASE))

def extract_sections(text: str) -> dict[str, str]:
    # Match lines like: ### 🧑‍🔬 Unit Testing
    section_regex = re.compile(
        r'^\s*#{2,6}\s*([\W_]{1,4} [^\n]+)\n([\s\S]*?)(?=^\s*#{2,6}\s*[\W_]{1,4} [^\n]+\n|$)',
        re.MULTILINE,
    )
    return {m.group(1).strip(): m.group(2).strip() for m in section_regex.finditer(text)}

def has_checked_box(text: str) -> bool:
    # Looks for - [x] or - [X] at the start of a line
    return bool(re.search(r'^\s*-\s*\[[xX]\]', text, re.MULTILINE))

def main() -> None:
    pr_body = os.environ.get('PR_BODY', '')
    if not pr_body:
        warn('PR description is missing! Please provide a valid PR description.')
        return

    template_path = os.path.join(os.environ.get('GITHUB_WORKSPACE', '.'), '.github', 'pull_request_template.md')
    try:
        with open(template_path, encoding='utf-8') as f:
            template_content = f.read().strip()
    except (FileNotFoundError, PermissionError, UnicodeDecodeError) as e:
        warn(f'Could not read pull_request_template.md for PR description validation: {e}')
        return

    template_sections = extract_sections(template_content)
    pr_sections = extract_sections(pr_body)

    if not template_sections:
        warn('Pull request template is empty or not formatted correctly.')
        return
    if not pr_sections:
        warn('Pull request description is empty. Please fill out the required sections.')
        return

    incomplete_sections = []
    for section, template_text in template_sections.items():
        pr_text = pr_sections.get(section, '')
        # Special handling for Unit Testing section with checkbox
        if "unit testing" in section.lower():
            if not has_checked_box(pr_text) and not is_na(pr_text):
                incomplete_sections.append(f"{section} (checkbox not checked or not marked as N/A)")
            continue
        if not normalize(pr_text) or normalize(pr_text) == normalize(template_text):
            if not is_na(pr_text):
                incomplete_sections.append(section)

    if incomplete_sections:
        warn(f'PR template requirements not met. Please complete the following section(s): {", ".join(incomplete_sections)}')
    else:
        print('PR description format is correct.')

if __name__ == "__main__":
    main()