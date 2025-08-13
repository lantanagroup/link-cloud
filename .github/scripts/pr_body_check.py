import os
import re

def warn(msg: str) -> None:
    # This will eventually show as a warning in the PR comments
    # when the script is run in a GitHub Actions workflow.
    print(f"::warning::{msg}")

def normalize(s: str) -> str:
    return s.replace('\r\n', '\n').replace('\r', '\n').strip()

def is_na(text: str) -> bool:
    # Accepts n/a, na, N/A, NA, n.a., N.A., etc. at the start, possibly followed by other text
    return bool(re.match(r'\s*(n[\./]?\s*a[\./]?|not\s*applicable)\b', text.strip(), re.IGNORECASE))

def extract_sections(text: str) -> dict[str, str]:
    # Match headings like: ## ... Description of Changes
    section_regex = re.compile(
        r'^\s*#{2,6}\s*.*?([A-Za-z ]+)\s*\n([\s\S]*?)(?=^\s*#{2,6}\s*.*?[A-Za-z ]+\s*\n|$)',
        re.MULTILINE,
    )
    return {m.group(1).strip().lower(): m.group(2).strip() for m in section_regex.finditer(text)}

def has_checked_box(text: str) -> bool:
    # Looks for - [x] or - [X] at the start of a line
    return bool(re.search(r'^\s*-\s*\[[xX]\]', text, re.MULTILINE))

def validate_template_sections(template_sections: dict[str, str], pr_sections: dict[str, str]) -> list[str]:
    """Validate PR sections against template sections and return incomplete sections."""
    incomplete_sections: list[str] = []
    for section, template_text in template_sections.items():
        # Skip the Unit Testing section entirely
        # until we have a better way to handle it, including the policy itself
        if "unit testing" in section.lower():
            continue
        pr_text = pr_sections.get(section, '')
        if not normalize(pr_text) or normalize(pr_text) == normalize(template_text):
            if not is_na(pr_text):
                incomplete_sections.append(section)
    return incomplete_sections

def main() -> None:
    pr_body = os.environ.get('PR_BODY', '')
    if not pr_body:
        warn('PR description is missing! Please provide a valid PR description.')
        return

    template_path = os.path.join(
        os.environ.get('GITHUB_WORKSPACE', '.'),
        '.github',
        'pull_request_template.md',
    )
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

    incomplete_sections = validate_template_sections(template_sections, pr_sections)

    if incomplete_sections:
        message = f"INFO: PR template requirements not met. Please complete the following section(s): {', '.join(incomplete_sections)}"
        # Write to GITHUB_OUTPUT for use in the workflow
        with open(os.environ['GITHUB_OUTPUT'], 'a') as fh:
            print(f'log={message}', file=fh)
        # Optionally, exit with non-zero code for failures (when policy is strict)
        # sys.exit(1)
    else:
        # Clear the log output if everything is fine
        with open(os.environ['GITHUB_OUTPUT'], 'a') as fh:
            print('log=', file=fh)
        print('INFO: PR description format is correct!')

if __name__ == "__main__":
    main()