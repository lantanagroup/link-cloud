# Overview

## Key Functionality

* Validates patient data, persists validation results
* Categorizes validation results
* Consumes Kafka events to determine when to validate patient data
* Produces Kafka events when validation is complete
* Provides a REST API to query validation results

## Building

### Manually

Ensure the local Maven repository has been specified in Maven settings.

```bash
mvn clean install
```

### Docker

```bash
docker build -t link-validation .
```

## Configured regular expressions

`valueSetWhitelistRegex` and category `RegexMatcher` expressions use
[RE2/J](https://github.com/google/re2j) so a configured expression cannot cause
catastrophic backtracking over clinical input.
Ordinary character classes, alternation, anchors and repetition are supported.
RE2/J supports a subset of Java regex syntax. Java-only constructs such as
backreferences, lookaround and possessive quantifiers are rejected when a rubric
or category rule is saved; existing expressions using them must be rewritten
before use.
An invalid stored whitelist is ignored, so the coding is still validated.
