PR summaries/titles must follow one of the two formats:
* TECH_DEBT: <brief_summary>
* LNK-XXX: <brief_summary>

Pull requests that have "TECH_DEBT" in the title should only contain changes related to typos, unused code, linter/IDE suggestions, swagger specification updates, documentation (such as markdown files) and logging improvements. These TECH_DEBT PRs must not affect core functionality. All PRs that are not considered technical debt must reference a JIRA ticket number in their summary for the LNK project, and include information on what testing was performed in the description of the PR.

The description of the PR must follow this template:

------
### 🛠️ Description of Changes
<description of changes here>

### 🧪 Testing Performed
<description of testing performed>

### 🧑‍🔬 Unit Testing
- [ ] I have written or updated unit tests to cover my changes

### 📓 Documentation Updated
<indication of documentation updated here>
------
