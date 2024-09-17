# measureeval

## Building for use as CLI

Perform the following from the `/Java` directory, which builds the measureeval JAR file _and_ the dependent shared
module:

```bash
mvn clean install -pl measureeval -am
```

## Running the CLI

To bypass the JAR manifest's main() class that runs it as a service, run the JAR with the following parameters:
`-Dloader.main=com.lantanagroup.link.measureeval.FileSystemInvocation org.springframework.boot.loader.launch.PropertiesLauncher`

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
java -cp measureeval.jar -Dloader.main=com.lantanagroup.link.measureeval.FileSystemInvocation org.springframework.boot.loader.launch.PropertiesLauncher "<measure-bundle-path>" "<patient-bundle-path>" "<start>" "<end>"
```

Example:

```bash
java -cp measureeval.jar -Dloader.main=com.lantanagroup.link.measureeval.FileSystemInvocation org.springframework.boot.loader.launch.PropertiesLauncher "C:/path/to/measure-bundle.json" "C:/path/to/patient-bundle.json" "2021-01-01" "2021-12-31"
```