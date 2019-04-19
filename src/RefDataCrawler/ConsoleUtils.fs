namespace RefDataCrawler

module ConsoleUtils=

    open System

    let private writeLine (color: ConsoleColor) (writer: System.IO.TextWriter) (text: string) =
        let fore = Console.ForegroundColor
        Console.ForegroundColor <- color
        writer.WriteLine(text)
        Console.ForegroundColor <- fore

    let private write (color: ConsoleColor) (writer: System.IO.TextWriter) (text: string) =
        let fore = Console.ForegroundColor
        Console.ForegroundColor <- color
        writer.Write(text)
        Console.ForegroundColor <- fore

    let error = writeLine ConsoleColor.Red Console.Error

    let info = writeLine ConsoleColor.White Console.Out
    
    let red  = write ConsoleColor.Red Console.Out 

    let white = write ConsoleColor.White Console.Out    

    let blue = write ConsoleColor.Cyan Console.Out

    let grey = write ConsoleColor.Gray Console.Out

    let green = write ConsoleColor.Green Console.Out

    let tabulate(values: seq<string * string>)=
        let max = values
                    |> Seq.map (fun (v,_) -> v.Length)
                    |> Seq.max

        let format (x:string,y) = (x.PadRight(max, ' '), y)

        values
        |> Seq.map format
    