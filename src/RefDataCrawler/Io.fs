namespace RefDataCrawler

module Io=
    open System.IO
    open System.Text

    let folder (path: string) = Path.GetDirectoryName(path)

    let path root subFolder = Path.Combine(root, subFolder)

    let filename (path: string) = Path.GetFileName(path)

    let relativePath (sourcePath: string) (filePath: string) = 
        let rootFolder = Path.GetDirectoryName(sourcePath)
        let fileFolder = Path.GetDirectoryName(filePath)
        
        let adjustedFolder = System.IO.Path.GetRelativePath(rootFolder, fileFolder)

        filePath |> filename |> path adjustedFolder
        

    let isRooted (path: string) = Path.IsPathRooted(path)

    let fileExists path = File.Exists path
    
    let createFolder path =
        async {
            let di = Directory.CreateDirectory(path)

            return di.FullName
        }
    
    let deleteFile path =
        if fileExists path then
            File.Delete path

    let writeString path (value: string) =
        async {
            use fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite)
            let fsw = new StreamWriter(fs, Encoding.UTF8)
            
            do! fsw.WriteAsync(value) |> Async.AwaitTask

            fsw.Flush()
            fs.Flush()
        }

    let readJson path =
        async {
            if fileExists path then
                use fs = new FileStream(path, FileMode.Open, FileAccess.Read)
                let fsr = new StreamReader(fs, true)

                let! json = fsr.ReadToEndAsync() |> Async.AwaitTask

                return Some json
            else
                return None
        }
