<Query Kind="Statements" />

/*
    This script is designed to resolve line ending issues in files that cause errors
    when executed or used in Linux environments. Common errors include `$'\r': command not found`
    or unexpected syntax errors caused by Windows-style line endings (CRLF: \r\n).

    The script performs the following tasks:
    1. Reads the specified file (or files in a directory).
    2. Converts Windows-style line endings (\r\n) to Unix-style line endings (\n).
    3. Overwrites the original file, or optionally saves the converted content to a new file.

    Usage:
    - Update the `filePath` variable to specify the path of the file to be converted.
    - Optionally, set the `fixedFilePath` variable to save the converted file as a new file
      (if left as null, the original file will be overwritten).
    - For batch processing, specify a directory in the batch version of the script and convert
      all matching files (e.g., `.sh` files).

    Notes:
    - This conversion ensures compatibility with bash scripts and similar tools on Linux.
    - Encoding is maintained as UTF-8 without a Byte Order Mark (BOM), as recommended for Linux compatibility.
*/


// Path to the file you want to convert
string filePath = Util.ReadLine("FilePath:", @"C:\path\to\your\file.sh");

// Optional: Specify a path for the fixed file (leave as `null` to overwrite the original file)
string fixedFilePath = null; // e.g., @"C:\path\to\your\file_fixed.sh";

// Read the entire file contents
string fileContent = File.ReadAllText(filePath);

// Convert Windows line endings (\r\n) to Unix line endings (\n)
string convertedContent = fileContent.Replace("\r\n", "\n");

// If no alternate path is provided, overwrite the original file
if (string.IsNullOrEmpty(fixedFilePath))
{
	File.WriteAllText(filePath, convertedContent, new System.Text.UTF8Encoding(false)); // Preserve UTF-8 encoding without BOM
	$"File '{filePath}' has been converted to Unix line endings.".Dump();
}
else
{
	// Save to a new file instead
	File.WriteAllText(fixedFilePath, convertedContent, new System.Text.UTF8Encoding(false));
	$"Converted file saved as '{fixedFilePath}'.".Dump();
}