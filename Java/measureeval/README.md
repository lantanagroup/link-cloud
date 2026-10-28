# measureeval

## Building for use as CLI

Perform the following from the `/Java` directory, which builds the measureeval JAR file _and_ the dependent shared
module:

```bash
mvn -P cli -pl measureeval -am clean package
```

We use the `cli` Maven profile to ensure that `FileSystemInvocation` is used as the main class.

### Parameters

| Parameter           | Description                                                                          |
|---------------------|--------------------------------------------------------------------------------------|
| measure-bundle-path | The path to the measure bundle JSON file or a directory of resource JSON/XML files.  |
| patient-bundle-path | The path to the patient bundle JSON file or a directory of resources JSON/XML files. |
| start               | The start date for the measurement period in FHIR Date or DateTime format.           |
| end                 | The end date for the measurement period in FHIR Date or DateTime format.             |

### Format/Example

Format:

```bash
java -jar measureeval-cli.jar "<measure-bundle-path>" "<patient-bundle-path>" "<start>" "<end>"
```

Example:

```bash
java -jar measureeval-cli.jar "C:/path/to/measure-bundle.json" "C:/path/to/patient-bundle.json" "2021-01-01" "2021-12-31"
```
