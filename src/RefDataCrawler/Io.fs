namespace RefDataCrawler

module Io=
    open System.IO
    open System.Text

    let folder (path: string) = Path.GetDirectoryName(path)

    let path root subFolder = Path.Combine(root, subFolder)

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

    let writeJson path (json: string) =
        async {
            use fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite)
            let fsw = new StreamWriter(fs, Encoding.UTF8)

            do! fsw.WriteAsync(json) |> Async.AwaitTask

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
