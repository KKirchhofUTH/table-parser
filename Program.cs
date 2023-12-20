// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Text.RegularExpressions;

String? line;
String[] arr;
StringBuilder outputString = new();
var outputType = OutputType.JSON; // JSON, HTML, TXT (WIP)
try
{   
    Console.WriteLine("Select source file location and name: ");
    Console.WriteLine("(example, C:\\Users\\<yourusername>\\Documents\\file_to_be_parsed.md)");
    Console.WriteLine("(alternatively, input just the filename to attempt to retrieve it from the Documents folder, like: file_to_be_parsed.md)");
    var input = Console.ReadLine() ?? "";
    //var input = @"C:\Users\kkirchhof\Documents\TableParser\input.md";
    // Set path for reading and writing (can technically be different, if needed). Default: current user's Documents folder (has -RW access generally)
    string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)+"\\";
    string docName = "input.md";
    var fullFilePath = $"{docPath}{docName}";
    if (input == ""){
        throw new Exception("Input empty.");
    }

    if (input.Contains('\\')){
        fullFilePath = input.Replace("\\", "\\\\");
    } else {
        fullFilePath = $"{docPath}{input}".Replace("\\", "\\\\");
    }
    //Pass the file path and file name to the StreamReader constructor
    StreamReader sr = new StreamReader(fullFilePath);
    // Extension type of file (decides RegEx to use)
    var extension = Path.GetExtension(fullFilePath);
    //Read the first line of text
    line = sr.ReadLine();

    // Disabled until HTML output has been made generic (to avoid id-/sourceIndex out of bounds stuff)
    // Console.WriteLine("Please select output type (defaults to JSON, if no number is input): ");
    // Console.WriteLine("1. JSON");
    // Console.WriteLine("2. HTML");
    // input = Console.ReadLine();

    // if (input != null){
    //     outputType = (OutputType)int.Parse(input);
    // }

    // Setup
    int idIndex = 3; // 0 index column number for where to get index name, for DEMOGRAPHICS, Target Column = 3
    int sourceIndex = 6; // 0 index column number for where to get source name, for DEMOGRAPHICS, Transformation Notes = 6
    string indent = "    "; // To keep indent consistent and easily changeable - default 4 spaces
    String[] colFullNames; // Full column names for table header
    String[] colIdNames; // Lower case, dash connected column names for td ids
    Regex regEx = new Regex(".*");
    if (extension == ".csv"){
        // CSV to HTML
        regEx = new Regex("[a-z0-9\\.\\*\\s\\-_]+,{1}|[^,].+|,|(\".+\")", RegexOptions.IgnoreCase); // Comma separated values, including empty and quote encapsuled ones
    } else if (extension == ".md"){    
        // MD to HTML
        regEx = new Regex("[^\\-\\|][a-z0-9\\.\\*\\s\\-_,'>\"+()%&@=$#!?/:]+", RegexOptions.IgnoreCase); // Pipe separated.
    }
    
    if(line != null){
        // Add initial output row, if needed
        switch (outputType)
        {
            case OutputType.JSON:
                outputString.AppendLine("```json:table");
                outputString.AppendLine("{");
                outputString.AppendLine(indent+"\"fields\" : [");
                break;
            case OutputType.HTML:
                outputString.AppendLine("<tr id=\"table-header\">");
                break;
            case OutputType.TXT:
                outputString.AppendLine();
                break;
            default:
                outputString.AppendLine();
                break;
        }

        // HEADER
        // First line should be column names
        colFullNames = regEx.Matches(line).OfType<Match>()
                            .Select(m => {
                            if(m.Groups[0].Value.EndsWith(',')){
                                return m.Groups[0].Value.Substring(0, m.Groups[0].Value.Length - 1).Replace("\"", "\\\""); // Remove delimiting comma, and add escape character to quotes
                            }
                            else if (m.Groups[0].Value.StartsWith('\"') && m.Groups[0].Value.EndsWith('\"'))
                            {
                                return m.Groups[0].Value.Substring(1, m.Groups[0].Value.Length - 2).Replace("\"", "\\\""); // Remove quotes added to encapsulate comma containing string, and add escape character to quotes
                            }
                            else 
                            {
                                return m.Groups[0].Value.Trim().Replace("\"", "\\\""); // Trim spaces (especially important with Markdown), and add escape character to quotes
                            }
                            })
                            .ToArray();

        colIdNames = new String[colFullNames.Length];
        for(var i = 0; i < colFullNames.Length; i++)
        {
            colIdNames[i] = colFullNames[i].ToLower().Replace(' ', '-');
            switch (outputType)
            {
                case OutputType.JSON:
                    outputString.Append(indent+indent+"{\"key\": \""+colIdNames[i]+"\", \"label\": \""+colFullNames[i]+"\", \"sortable\": true}");
                    if (i == colFullNames.Length - 1) {// If last line, don't add ","
                        outputString.AppendLine();
                        break;
                    }

                    outputString.AppendLine(",");
                    break;
                case OutputType.HTML:
                    outputString.AppendLine(indent+$"<th id=\"{colIdNames[i]}-header\">{colFullNames[i]}</th>");
                    break;
                case OutputType.TXT:
                    outputString.AppendLine();
                    break;
                default:
                    outputString.AppendLine();
                    break;
            }
        }

        switch (outputType)
        {
            case OutputType.JSON:
                outputString.AppendLine(indent+"],");
                break;
            case OutputType.HTML:
                outputString.AppendLine("</tr>");
                break;
            case OutputType.TXT:
                outputString.AppendLine();
                break;
            default:
                outputString.AppendLine();
                break;
        }
        // END HEADER

        // TABLE ROWS
        if (outputType == OutputType.JSON)
            outputString.AppendLine(indent+"\"items\" : [");
        line = sr.ReadLine();
        while (line != null)
        {
            arr = regEx.Matches(line).OfType<Match>()
                            .Select(m => {
                            if(m.Groups[0].Value.EndsWith(',')){
                                return m.Groups[0].Value.Substring(0, m.Groups[0].Value.Length - 1).Replace("\"", "\\\""); // Remove delimiting comma, and add escape character to quotes
                            }
                            else if (m.Groups[0].Value.StartsWith('\"') && m.Groups[0].Value.EndsWith('\"'))
                            {
                                return m.Groups[0].Value.Substring(1, m.Groups[0].Value.Length - 2).Replace("\"", "\\\""); // Remove quotes added to encapsulate comma containing string, and add escape character to quotes
                            }
                            else 
                            {
                                return m.Groups[0].Value.Trim().Replace("\"", "\\\""); // Trim spaces (especially important with Markdown), and add escape character to quotes
                            }
                            })
                            .ToArray();
            // Skip "empty" lines
            if (arr.Length == 0 && sr.ReadLine() != null){
                line = sr.ReadLine();
                continue;
            }
            
            if(arr.Length != colFullNames.Length)
                throw new Exception($"Column size mismatch between header row and subsequent data row, {colFullNames.Length} vs {arr.Length}, with {arr[arr.Length-1]}");

            // Get source name || TODO: Make this generic for general use
            var rxCheckForSource = new Regex("Epic|MHH|AllScripts|GECBI|All data sources");
            var source = rxCheckForSource.Match(arr[sourceIndex]).Value.Trim().Replace(" ", "-");

            // Create row
            switch (outputType)
            {
                case OutputType.JSON:
                    outputString.Append(indent+indent+"{");
                    for (var i = 0; i < arr.Length; i++)
                    {
                        outputString.Append("\""+colIdNames[i]+"\": \""+arr[i]+"\"");

                        if(i != arr.Length - 1){
                            outputString.Append(", ");
                        }
                    }
                    outputString.AppendLine("},");
                    break;
                case OutputType.HTML:
                    outputString.AppendLine($"<tr id=\"{arr[idIndex].ToLower()}-{source.ToLower()}\">");
                    for (var i = 0; i < arr.Length; i++)
                    {
                        outputString.AppendLine(indent+$"<td id=\"{colIdNames[i].ToLower()}-{arr[idIndex].ToLower()}-{source.ToLower()}\">{arr[i]}</td>");
                    }
                    outputString.AppendLine("</tr>");
                    break;
                case OutputType.TXT:
                    outputString.AppendLine();
                    break;
                default:
                    outputString.AppendLine();
                    break;
            }
                
            //Read the next line
            line = sr.ReadLine();
        }
        // END TABLE ROWS

        // Finalize if needed (JSON)
        if (line == null && outputType == OutputType.JSON){
            outputString.Remove(outputString.Length-1, 1); // Remove last comma
            outputString.AppendLine(indent+"],");
            outputString.AppendLine(indent+"\"filter\" : true"); // Add search box
            outputString.AppendLine("}");
            outputString.AppendLine("```");
        }
        Console.WriteLine(outputString); 

        // Output to file
        using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, $"WriteLines_{outputType}_{DateTime.Now.Date.Year+""+DateTime.Now.Date.Month+""+DateTime.Now.Date.Day}.txt")))
        {
                outputFile.WriteLine(outputString);
        }
    }

    //close the file
    sr.Close();
    Console.ReadLine();
}
catch(Exception e)
{
    Console.WriteLine("Exception: " + e.Message);
}
finally
{
    Console.WriteLine("Executing finally block.");
}

enum OutputType {
    TXT,
    JSON,
    HTML
}
