import os
import sys
import re

def fail(msg):
    print(msg)
    sys.exit(1)

def normalize(s):
    return s.replace('\r\n', '\n').replace('\r', '\n').strip()

def main():
    pr_title = os.environ.get('PR_TITLE', '')
    pr_body = os.environ.get('PR_BODY', '')

    # Title check
    title_pattern = re.compile(r'(^LNK-\d+:\s)|(^TECH_DEBT:\s)')
    if not title_pattern.search(pr_title):
        fail('Invalid PR title! Must begin with LNK-nnnn:<space> or TECH_DEBT:<space>, e.g. LNK-1234: My PR title, or TECH_DEBT: My PR title')
    else:
        print('PR title format is correct.')

    # Description check
    template_path = os.path.join(os.environ.get('GITHUB_WORKSPACE', '.'), '.github', 'pull_request_template.md')
    try:
        with open(template_path, encoding='utf-8') as f:
            template_content = f.read().strip()
    except Exception as e:
        fail(f'Could not read pull_request_template.md for PR description validation: {e}')

    # Split template into sections by headings (e.g., ### Section)
    section_regex = re.compile(r'###\s+(.+)\n([\s\S]*?)(?=\n###|$)')
    incomplete_sections = []
    for match in section_regex.finditer(template_content):
        section_title = match.group(1).strip()
        template_section_content = match.group(2).strip()

        pr_section_regex = re.compile(rf'###\s+{re.escape(section_title)}\n([\s\S]*?)(?=\n###|$)')
        pr_match = pr_section_regex.search(pr_body or '')
        pr_section_content = pr_match.group(1).strip() if pr_match else ''

        if not pr_section_content or pr_section_content == template_section_content:
            incomplete_sections.append(section_title)

    if incomplete_sections:
        fail(f'PR template requirements not met. Please complete the following section(s): {", ".join(incomplete_sections)}')
    else:
        print('PR description format is correct.')

if __name__ == "__main__":
    main()