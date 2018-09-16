module ElmFileGenerator
open FSharp.Data;
open System.IO;
open FileHandling;

let newFile = __SOURCE_DIRECTORY__ + @"/../../elm-docs/src/Generated/AllElmDocs.elm"
let decoderFile = __SOURCE_DIRECTORY__ + @"/../../elm-docs/src/Generated/AllElmDocsDecoders.elm"

let rec cleanupComments json =
    match json with
    | JsonValue.String s -> json
    | JsonValue.Number d -> json
    | JsonValue.Float f -> json
    | JsonValue.Boolean _  | JsonValue.Null -> json
    | JsonValue.Record props ->
      props
      |> Array.map (fun (key, value) -> key,
                                          if key.Equals("comment") then JsonValue.String ""
                                          else cleanupComments value)
                                          |> JsonValue.Record
    | JsonValue.Array array ->
      array
      |> Array.map cleanupComments
      |> JsonValue.Array


let cleanAndWriteToStream (elmDocsWriter:StreamWriter) (elmDecoderWriter:StreamWriter) name (fullPath:string) =
    match parseFileNameToPackageMetadata name with
    | Some (vendorName, packageName, _) ->
        let jsonFile = JsonValue.Load(fullPath)
        let cleanJson = cleanupComments jsonFile
        let sourcePackageName = "p_" + (vendorName.ToLower().Replace("-", "_")) + "_" + packageName.Replace("-", "_")
        let decoderSourcePackageName = "decode_" + sourcePackageName
        let packageNameLine = sprintf @"%s = """""" " sourcePackageName
        elmDocsWriter.WriteLine packageNameLine
        cleanJson.WriteTo (elmDocsWriter, JsonSaveOptions.DisableFormatting)
        elmDocsWriter.WriteLine @""""""""
        elmDocsWriter.WriteLine ""

        elmDecoderWriter.WriteLine (sprintf @"%s = Json.Decode.decodeString (Json.Decode.list decoder) %s" decoderSourcePackageName sourcePackageName)
        elmDecoderWriter.WriteLine ""
        sprintf @"(""%s/%s"", %s)" vendorName packageName decoderSourcePackageName

    | None ->
        ""


let generateElmFilesFromCachedPackages rootPath =
    use elmDocsWriter = new StreamWriter(newFile)
    elmDocsWriter.WriteLine "-- WARNING - File generated by PackageCache tool"
    elmDocsWriter.WriteLine (sprintf "-- Generated on: " + System.DateTime.Now.ToString() )
    elmDocsWriter.WriteLine "module Generated.AllElmDocs exposing (..)"
    elmDocsWriter.WriteLine ""

    use elmDecoderWriter = new StreamWriter(decoderFile)
    elmDecoderWriter.WriteLine "-- WARNING - File generated by PackageCache tool"
    elmDecoderWriter.WriteLine (sprintf "-- Generated on: " + System.DateTime.Now.ToString() )
    elmDecoderWriter.WriteLine "module Generated.AllElmDocsDecoders exposing (..)"
    elmDecoderWriter.WriteLine ""
    elmDecoderWriter.WriteLine "import Json.Decode exposing (..)"
    elmDecoderWriter.WriteLine "import Elm.Docs exposing (..)"
    elmDecoderWriter.WriteLine "import Generated.AllElmDocs exposing (..)"
    elmDecoderWriter.WriteLine ""

    let decoderList =
        getFiles rootPath
        |> Seq.map (fun file -> cleanAndWriteToStream elmDocsWriter elmDecoderWriter file.Name file.FullName)
        |> Seq.reduce (fun acc newVal -> acc + ", " + newVal)
        |> sprintf @"decoderList = [%s]"

    elmDecoderWriter.WriteLine decoderList
    printfn "All packages generated to Elm files successfully."