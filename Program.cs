// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Text.RegularExpressions;

String? line;
String[] arr = [];
StringBuilder outputString = new();
List<Array> txtArrays = new List<Array>();
var outputType = OutputType.JSON; // JSON, HTML, TXT (WIP)
var outputStyle = OutputStyle.GitLabTable; // GitLabTable, OpenProjectsTable
try
{   
    Console.WriteLine("Select source file location and name: ");
    Console.WriteLine("(example, C:\\Users\\<yourusername>\\Documents\\file_to_be_parsed.md)");
    Console.WriteLine("(alternatively, input just the filename to attempt to retrieve it from the Documents folder, like: file_to_be_parsed.md)");
    var input = Console.ReadLine() ?? "";
    //var input = @"C:\Users\kkirchhof\Documents\input.txt";
    // Set path for reading and writing (can technically be different, if needed). Default: current user's Documents folder (has -RW access generally)
    string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)+"\\";
    string docName = "input.json";
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
    String[] colFullNames = []; // Full column names for table header
    String[] colIdNames; // Lower case, dash connected column names for td ids
    Regex regEx = new Regex(".*");
    if (extension == ".csv"){
        // CSV input
        regEx = new Regex("[a-z0-9\\.\\*\\s\\-_]+,{1}|[^,].+|,|(\".+\")", RegexOptions.IgnoreCase); // Comma separated values, including empty and quote encapsuled ones
    } else if (extension == ".md"){    
        // MD input
        regEx = new Regex("[^\\-\\|][a-z0-9\\.\\*\\s\\-_,'>\"+()%&@=$#!?/:]+", RegexOptions.IgnoreCase); // Pipe separated.
    } else if (extension == ".json"){
        // table-parser JSON table input
        // Does not use RegEx, but rather IndexOf
    } else if (extension == ".txt"){
        // Assuming tab-delimited file, as that is what SSMS outputs to .txt
        regEx = new Regex("[a-z0-9()_-]+", RegexOptions.IgnoreCase);
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
                outputString.AppendLine("<table>");
                outputString.AppendLine(indent+"<tr id=\"table-header\">");
                break;
            case OutputType.TXT:
                outputString.AppendLine("HEADER");
                break;
            default:
                outputString.AppendLine();
                break;
        }

        // HEADER
        // MD or CSV input
        // First matched line should be column names
        if (extension == ".md" || extension == ".csv") {
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
        } 
        // JSON input
        else if (extension == ".json"){
            while (line != null && !line.Contains("fields")) // Keep going through the initial lines until we find "fields"
                line = sr.ReadLine();

            line = sr.ReadLine(); // First line would be the line after "fields"
            var colFullNamesList = new List<string>();
            while (line != null && !line.Trim().StartsWith("],")) {// Get header names until we reach end of "fields" (presumably with a line starting with "],")
                var indexStart = line.IndexOf("label");
                var indexEnd = line.IndexOf("sortable") > -1 ? line.IndexOf("sortable") - 6 : line.IndexOf("}") - 2;

                if(indexStart == -1)
                    throw new Exception("JSON file does not contain 'label' within the 'fields' section.");

                colFullNamesList.Add(line.Substring(indexStart + 9, indexEnd - (indexStart + 7))); // Start index + characters until actual label name, length of characters + 1 to consider 0 index
                line = sr.ReadLine();
            }
            colFullNames = colFullNamesList.ToArray();
        } 
        // TXT (tab-delimited, new line per column) input
        // Have to read full text right away, as all lines contain column names, but also potential values
        else if (extension == ".txt") {
            var colFullNamesList = new List<string>();
            while (line != null) {
                var row = regEx.Matches(line).OfType<Match>()
                                .Select(m => m.Groups[0].Value)
                                .ToArray();
                colFullNamesList.Add(row[0]);
                txtArrays.Add(row);
                line = sr.ReadLine();
            }
            colFullNames = colFullNamesList.ToArray();
        }

        colIdNames = new String[colFullNames.Length];
        // GitLabTable output (default)
        if (outputStyle == OutputStyle.GitLabTable) {
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
                        outputString.AppendLine(indent+indent+$"<th id=\"{colIdNames[i]}-header\">{colFullNames[i]}</th>");
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
                    outputString.AppendLine(indent+"</tr>");
                    break;
                case OutputType.TXT:
                    outputString.AppendLine();
                    break;
                default:
                    outputString.AppendLine();
                    break;
            }
        } else if (outputStyle == OutputStyle.OpenProjectsTable) {
            for(var i = 0; i < colFullNames.Length; i++)
            {
                colIdNames[i] = colFullNames[i].ToLower().Replace(' ', '-');
            }
            outputString.AppendLine(indent+indent+"<th id=\"column-name-header\">Column name</th>");
            outputString.AppendLine(indent+indent+"<th id=\"description-header\">Description</th>");
            outputString.AppendLine(indent+indent+"<th id=\"notes-header\">Notes</th>");
            outputString.AppendLine(indent+"</tr>");
        }
        // END HEADER

        // TABLE ROWS
        if (outputType == OutputType.JSON)
            outputString.AppendLine(indent+"\"items\" : [");
        
        if (outputStyle == OutputStyle.GitLabTable) {
            line = sr.ReadLine();
            while (line != null)
            {
                // MD or CSV input
                if (extension == ".md" || extension == ".csv") {
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
                    if (arr.Length == 0 && (line = sr.ReadLine()) != null){
                        continue;
                    }
                } else if (extension == ".json") {
                    if (line != null && line.Contains("\"items\" : [")) // Skip the "items" line
                        line = sr.ReadLine();

                    // If reaching end of items, denoted by "],", ensure line is null (for finalizing for JSON input, as there are technically more non-empty lines) and break the loop
                    if (line == null || line.Trim().StartsWith("],")) {
                        line = null;
                        break;
                    }

                    var itemsList = new List<string>();
                    for(var i = 0; i < colIdNames.Length; i++) {
                        var indexStart = line.IndexOf(colIdNames[i]) + colIdNames[i].Length + 4;
                        var indexEnd = i + 1 >= colIdNames.Length ? line.IndexOf("},") > -1 ? line.Length - 3 : line.Length - 2 : line.IndexOf(colIdNames[i + 1]) - 4;

                        if(indexStart == -1)
                            throw new Exception($"JSON file does not contain '{colIdNames[i]}' within the 'items' section.");

                        itemsList.Add(line.Substring(indexStart, indexEnd - indexStart)); // Start index + characters until actual label name, length characters + 1 to consider 0 index
                    }
                    arr = itemsList.ToArray();
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
                        outputString.AppendLine(indent+$"<tr id=\"{arr[idIndex].ToLower()}-{source.ToLower()}\">");
                        for (var i = 0; i < arr.Length; i++)
                        {
                            outputString.AppendLine(indent+indent+$"<td id=\"{colIdNames[i].ToLower()}-{arr[idIndex].ToLower()}-{source.ToLower()}\">{arr[i]}</td>");
                        }
                        outputString.AppendLine(indent+"</tr>");
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
        } else if (outputStyle == OutputStyle.OpenProjectsTable) {
            for (var i = 0; i < colFullNames.Length; i++)
            {
                outputString.AppendLine(indent+$"<tr id=\"{colIdNames[i]}\">");
                outputString.AppendLine(indent+indent+$"<td>{colFullNames[i]}</td>");
                if (txtArrays.Count > 0 && txtArrays[i] != null && txtArrays[i].GetValue(1) != null){
                    if (txtArrays[i].GetValue(2) != null && txtArrays[i].GetValue(2).ToString().ToLower() == "unchecked")
                        outputString.AppendLine(indent+indent+$"<td>{txtArrays[i].GetValue(1)}, not null</td>");
                    else 
                        outputString.AppendLine(indent+indent+$"<td>{txtArrays[i].GetValue(1)}</td>");
                }
                else {
                    outputString.AppendLine(indent+indent+$"<td></td>");
                }
                outputString.AppendLine(indent+indent+$"<td></td>");
                outputString.AppendLine(indent+"</tr>");
            }
        }
        // END TABLE ROWS

        // Finalize if needed (JSON or HTML)
        if (line == null && outputType == OutputType.JSON) {
            outputString.Remove(outputString.Length-1, 1); // Remove last comma
            outputString.AppendLine(indent+"],");
            outputString.AppendLine(indent+"\"filter\" : true"); // Add search box
            outputString.AppendLine("}");
            outputString.AppendLine("```");
        } else if (line == null && outputType == OutputType.HTML) {
            outputString.AppendLine("</table>");
        }

        Console.WriteLine(outputString); 

        // Output to file
        var date = DateTime.Now.Date.Year.ToString() + (DateTime.Now.Date.Month < 10 ? "0" : "") + DateTime.Now.Date.Month.ToString() + (DateTime.Now.Date.Day < 10 ? "0" : "") + DateTime.Now.Date.Day.ToString();
        using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, $"WriteLines_{outputStyle}_{outputType}_{date}.txt")))
        {
                outputFile.WriteLine(outputString);
        }
    }

    //close the file
    sr.Close();
    //Console.ReadLine(); Uncomment to leave program running until enter is hit in the console
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

enum OutputStyle {
    GitLabTable,
    OpenProjectsTable
}
