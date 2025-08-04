import os
import sys
import re

def fail(msg):
    print(msg)
    sys.exit(1)

def normalize(s):
    return s.replace('\r\n', '\n').replace('\r', '\n').strip()

def extract_sections(text):
    # Match lines starting with optional whitespace, an emoji or symbol, a space, and heading text
    section_regex = re.compile(
        r'^\s*([\W_]{1,4} [^\n]+)\n([\s\S]*?)(?=^\s*[\W_]{1,4} [^\n]+\n|$)',
        re.MULTILINE
    )
    return {m.group(1).strip(): m.group(2).strip() for m in section_regex.finditer(text)}

def check_checklist_items(pr_body, required_items):
    """
    Checks if required checklist items are present and checked.
    Returns a list of items that are missing or unchecked.
    """
    missing_or_unchecked = []
    for item in required_items:
        # Regex to match checked or unchecked box for the item
        pattern = re.compile(r'- \[([ xX])\] ' + re.escape(item))
        match = pattern.search(pr_body)
        if not match or match.group(1) != 'x':
            missing_or_unchecked.append(item)
    return missing_or_unchecked

def main():
    pr_title = os.environ.get('PR_TITLE', '')
    pr_body = os.environ.get('PR_BODY', '')
    if not pr_title:
        fail('PR title is missing! Please provide a valid PR title.')
    if not pr_body:
        fail('PR description is missing! Please provide a valid PR description.')

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

    template_sections = extract_sections(template_content)
    pr_sections = extract_sections(pr_body or '')

    if not template_sections:
        fail('Pull request template is empty or not formatted correctly.')
    if not pr_sections:
        fail('Pull request description is empty. Please fill out the required sections.')

    incomplete_sections = []
    for section, template_text in template_sections.items():
        pr_text = pr_sections.get(section, '')
        # Normalize both texts before comparison
        print(f'Checking section: {section}')
        print(f'PR text: "{pr_text}"')
        print(f'Template text: "{template_text}"')
        if not normalize(pr_text) or normalize(pr_text) == normalize(template_text):
            incomplete_sections.append(section)

    if incomplete_sections:
        fail(f'PR template requirements not met. Please complete the following section(s): {", ".join(incomplete_sections)}')
    else:
        print('PR description format is correct!!!')

    # After section checks, add checklist validation
    required_checklist_items = [
        "I have written or updated unit tests to cover my changes"
        # Add more checklist items here if needed
    ]
    checklist_issues = check_checklist_items(pr_body, required_checklist_items)
    if checklist_issues:
        fail(f'PR checklist requirements not met. Please check the following item(s): {", ".join(checklist_issues)}')

if __name__ == "__main__":
    main()