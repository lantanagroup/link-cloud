import os
import sys
import re

def fail(msg):
    print(msg)
    sys.exit(1)

def normalize(s):
    return s.replace('\r\n', '\n').replace('\r', '\n').strip()

def extract_sections(text):
    # Match lines starting with an emoji and heading, then capture content until the next such heading or end
    section_regex = re.compile(
        r'^([\U0001F300-\U0001FAFF][^\n]+)\n([\s\S]*?)(?=^[\U0001F300-\U0001FAFF][^\n]+\n|$)',
        re.MULTILINE
    )
    return {m.group(1).strip(): m.group(2).strip() for m in section_regex.finditer(text)}

def main():
    pr_title = os.environ.get('PR_TITLE', '')
    pr_body = os.environ.get('PR_BODY', '')
    print('%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%')
    print(f'PR_TITLE: {pr_title}')
    print(f'PR_BODY: {pr_body}')
    print('%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%')
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

    print('oooooooooooooooooooooooooooooooooooooooo')
    print(f'template_path: {template_path}')
    print(f'template_content: {template_content}')
    print('oooooooooooooooooooooooooooooooooooooooo')

    template_sections = extract_sections(template_content)
    print('$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$')
    print(f'template_sections: {template_sections}')
    print('$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$')
    pr_sections = extract_sections(pr_body or '')

    if not template_sections:
        print('Pull request template is empty or not formatted correctly.')
    if not pr_sections:
        print('Pull request description is empty. Please fill out the required sections.')

    print (f'Length of template sections: {len(template_sections)}')
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

if __name__ == "__main__":
    main()