namespace RefDataCrawler

open System
open System.IO

type SdeCrawler(config: SdeCrawlerConfig) =
    
    let prepareUnpack() =
        let di = new DirectoryInfo(config.targetPath)
        if di.Exists then
            di.EnumerateDirectories() |> Seq.iter (fun x -> x.Delete(true))
            di.EnumerateFiles() |> Seq.iter (fun x -> x.Delete())
            
        else
            Directory.CreateDirectory(config.targetPath) |> ignore

        let newPath = Path.Combine(config.targetPath, "temp")
        Directory.CreateDirectory(newPath).FullName
        

    let downloadZip(folderPath: string) =
        let filePath = Path.Combine(folderPath, "sde.zip")
        use c = new System.Net.WebClient()
        c.DownloadFile(config.sourcePath, filePath)
        filePath

    let unpackZip(filePath: string)=
        
        System.IO.Compression.ZipFile.ExtractToDirectory(filePath, config.targetPath)
        Path.GetDirectoryName(filePath)

    let cleanupPackage(tempFolderPath: string)=
        Directory.Delete(tempFolderPath, true)
        true

    member this.Start() = prepareUnpack() |> downloadZip |> unpackZip |> cleanupPackage
        
    