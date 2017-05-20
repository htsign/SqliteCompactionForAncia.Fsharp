module Program

open System
open System.Diagnostics
open System.IO
open System.IO.Compression
open System.Linq
open System.Net
open System.Reflection
open System.Text
open System.Text.RegularExpressions
open RegExp

exception NotFoundException of string
let Here = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName

[<EntryPoint>]
let main _ =
    let exefilePattern = @"^(sqlite\d*\.exe)$"
    
    let download (url : string) =
        use wc = new WebClient(BaseAddress = "http://www.sqlite.org")
        wc.DownloadData url
    
    let fetchExecutable dest =
        let getZipUrl = function
            | Match @"\('a\d+?','(\d+/sqlite\-tools\-win32\-x86(?:\-\d+?)?\.zip)'\)" [href] -> href
            | _ -> raise (NotFoundException "An archive file is not found in download page.")
        
        let pickEntryFromArchive pattern stream =
            use archive = new ZipArchive(stream)
            let files = archive.Entries |> Seq.choose (fun entry ->
                match entry.Name with
                | Match pattern [name] -> Some entry
                | _ -> None)
            match List.ofSeq files with
            | [] -> raise (NotFoundException "An entry is not found in archive.")
            | entry :: _ -> entry
            
        // exeファイル入りzipのURLを取得
        let url = download "download.html" |> Encoding.UTF8.GetString |> getZipUrl
        
        printf "downloading..."
        let data = download url
        printfn " done."
        
        let entry = new MemoryStream(data) |> pickEntryFromArchive exefilePattern
        let path = Path.Combine(dest, entry.Name)
        
        printf "extracting..."
        entry.ExtractToFile path
        printfn " done."
        path
    
    let sqlite =
        let isExe file = Regex.IsMatch(file, exefilePattern)
        let files = Directory.EnumerateFiles Here |> Seq.filter isExe |> Seq.toList
        match files with
        | file :: _ -> file
        | [] ->
            // 無かったらネットから取ってくる
            try fetchExecutable Here with NotFoundException msg ->
                printfn "%s" msg
                Environment.Exit(1)
                reraise ()
    
    let compact path =
        try
            let psi = ProcessStartInfo(sqlite, path + " vacuum")
            psi.UseShellExecute <- false
            psi.CreateNoWindow  <- true
            
            // コンパクション実行
            printf "compacting %s..." <| Path.GetFileName path
            use proc = new Process(StartInfo = psi)
            proc.Start() |> ignore
            proc.WaitForExit()
            printfn " done."
        with
            | _ -> printfn " failed."
    
    let isDb name = [".db"; ".db3"] |> List.contains (Path.GetExtension name)
    let files = Directory.EnumerateFiles(Here, "*", SearchOption.AllDirectories)
    
    files |> Seq.filter isDb |> Seq.iter compact
    0
