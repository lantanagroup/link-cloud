import os
import sys
import re

def fail(msg):
    print(msg)
    sys.exit(1)

def main():
    pr_title = os.environ.get('PR_TITLE', '')
    if not pr_title:
        fail('PR title is missing! Please provide a valid PR title.')

    title_pattern = re.compile(r'(^LNK-\d+:\s)|(^TECH_DEBT:\s)')
    if not title_pattern.search(pr_title):
        fail('Invalid PR title! Must begin with LNK-nnnn:<space> or TECH_DEBT:<space>, e.g. LNK-1234: My PR title, or TECH_DEBT: My PR title')
    else:
        print('PR title format is correct.')

if __name__ == "__main__":
    main()