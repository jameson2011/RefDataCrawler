#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

let publishOptions = (fun (opts: DotNet.PublishOptions) -> 
                          {
                            opts with 
                                Configuration = DotNet.BuildConfiguration.Release
                                OutputPath = Some "../../publish"
                          }


                    )

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    |> Shell.cleanDirs 
)

Target.create "Build" (fun _ ->
    !! "src/**/*.*proj"
    |> Seq.iter (DotNet.build id)
)

Target.create "Publish" (fun _ -> 
    !! "src/**/*.*proj"
    |> Seq.iter (DotNet.publish publishOptions)
)

Target.create "All" ignore

"Clean"
  ==> "Build"
  ==> "Publish"
  ==> "All"

Target.runOrDefault "All"
