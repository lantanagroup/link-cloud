## Overview

This system is a high-performing, big-data platform intended to support collecting data directly from multiple hospitals/facilities/EHRs, normalizing the data (when minimally necessary), evaluating it using measure/CQL logic, and submitting the data to down-stream systems in a well-defined structure/format.

## General Guidance

* Logging messages in functionality that repeat excessively (such as consumption of ResourceNormalized events - which occurs for every resource the system process, millions of times) should be treated as DEBUG messages or the namespace/package should be ignored by default so that it does not produce excessive persisted log messages.
* The Scripts directory contains scripts used by developers and admins to help operate the system at runtime. These scripts must always abstract out sensitive variables into arguments. Arguments may default values to environment variables for ease-of use, when appropriate.

## Pull Requests

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
