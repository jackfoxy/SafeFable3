
module Build.Tasks

open BlackFox.Fake
open System.IO
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.IO.Globbing.Operators
open Fake.JavaScript

// let yarn = 
//     if Environment.isWindows then "yarn.cmd" else "yarn"
//     |> ProcessUtils.tryFindFileOnPath
//     |> function
//        | Some yarn -> yarn
//        | ex -> failwith ( sprintf "yarn not found (%A)\n" ex )

// let gitName = "MetaAggregator"
// let gitOwner = "jackfoxy"
// let gitHome = sprintf "https://github.com/%s" gitOwner

// // Filesets
// let projects  =
//       !! "src/**.fsproj"

let project = "SafeFable3"
let summary = "SAFE-Dojo solution with Fable 3"
let configuration = "Release"

let serverPath = Path.getFullName "./src/Server"
let clientPath = Path.getFullName "./src/Client"
let clientDeployPath = Path.combine clientPath "deploy"
let deployDir = Path.getFullName "./deploy"

let platformTool tool winTool =
    let tool = if Environment.isUnix then tool else winTool
    match ProcessUtils.tryFindFileOnPath tool with
    | Some t -> t
    | _ ->
        let errorMsg =
            tool + " was not found in path. " +
            "Please install it and make sure it's available from your path. " +
            "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
        failwith errorMsg

type JsPackageManager = 
    | NPM
    | YARN
    member this.RestoreTool =
        match this with
        | NPM -> platformTool "npm" "npm.cmd"
        | YARN -> platformTool "yarn" "yarn.cmd"
    member this.RunTool =
        match this with
        | NPM -> platformTool "npx" "npx.cmd"
        | YARN -> platformTool "yarn" "yarn.cmd"
    member this.ArgsInstall =
        match this with
        | NPM -> "install"
        | YARN -> "install --frozen-lockfile"

let getJsPackageManager () = 
    match Environment.environVarOrDefault "jsPackageManager" "yarn" with
    | "npm" -> NPM
    | "yarn" | _ -> YARN

let nodeTool = platformTool "node" "node.exe"

let runTool cmd args workingDir =
    let arguments = args |> String.split ' ' |> Arguments.OfArgs
    Command.RawCommand (cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let runDotNet cmd workingDir =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

let openBrowser url =
    //https://github.com/dotnet/corefx/issues/10361
    Command.ShellCommand url
    |> CreateProcess.fromCommand
    |> CreateProcess.ensureExitCodeWithMessage "opening browser failed"
    |> Proc.run
    |> ignore

let globToArray x =
    !! x
    |> Seq.map id
    |> Seq.toArray

let createAndGetDefault () =

    let clean = BuildTask.create "Clean" [] {
        [|
            //globToArray "**/src/**/bin"
            //globToArray "**/src/**/obj"
            //globToArray "**/tests/**/bin"
            //globToArray "**/tests/**/obj"
            [|"temp"|]
        |]
        |> Array.collect id
        |> Array.append [|deployDir; clientDeployPath|]
        |> Shell.cleanDirs
        }

    //let cleanDocs = BuildTask.create "CleanDocs" [] {
    //    Shell.cleanDirs ["../docs/reference"; "docs"]
    //    }
        
    let installClient = BuildTask.create "InstallClient" [] {
         let jsPackageManager = getJsPackageManager ()

         printfn "Node version:"
         runTool nodeTool "--version" __SOURCE_DIRECTORY__
         printfn "Npm version:"
         runTool jsPackageManager.RestoreTool "--version"  __SOURCE_DIRECTORY__
         runTool jsPackageManager.RestoreTool "install" __SOURCE_DIRECTORY__
         runDotNet "restore" clientPath
     }

    // Generate assembly info files with the right version & up-to-date information
    //let assemblyInfo = BuildTask.create "AssemblyInfo" [clean] {
    //    let getAssemblyInfoAttributes projectName =
    //        [   AssemblyInfo.Title (projectName)
    //            AssemblyInfo.Product project
    //            AssemblyInfo.Description summary
    //            AssemblyInfo.Configuration configuration ]

    //    let getProjectDetails (projectPath :string) =
    //        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    //        ( projectPath,
    //            projectName,
    //            System.IO.Path.GetDirectoryName(projectPath),
    //            (getAssemblyInfoAttributes projectName)
    //        )

    //    !! "src/**/*.??proj"
    //    |> Seq.map getProjectDetails
    //    |> Seq.iter (fun (projFileName, _, folderName, attributes) ->
    //        match projFileName with
    //        | Fsproj -> AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes
    //        | Csproj -> AssemblyInfoFile.createCSharp ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
    //        | Vbproj -> AssemblyInfoFile.createVisualBasic ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
    //        | Shproj -> ()
    //        )
    //}

    //let buildConfiguration = DotNet.Custom <| Environment.environVarOrDefault "configuration" configuration

    // --------------------------------------------------------------------------------------
    // Build library & test project

    let build = BuildTask.create "Build" [clean] {
        let jsPackageManager = getJsPackageManager ()

        //projectsToBuild
        //|> Array.iter (fun x -> 
        //    DotNet.build (fun p ->
        //        { p with
        //            Configuration = buildConfiguration 
        //            DotNet.BuildOptions.MSBuildParams = 
        //                { p.MSBuildParams  with
        //                    DisableInternalBinLog = true }
        //        }) x
        //)
               
        runDotNet "build" serverPath
        runTool jsPackageManager.RunTool "webpack-cli -p" __SOURCE_DIRECTORY__
    }

    //let buildTests = BuildTask.create "BuildTests" [assemblyInfo] {
    //    [|
    //        globToArray "**/tests/**/bin"
    //        globToArray "**/tests/**/obj"
    //    |]
    //    |> Array.collect id
    //    |> Shell.cleanDirs
       
    //    [|
    //        Path.getFullName <| sprintf "./tests/%s.Tests" project
    //        Path.getFullName "./tests/Benchmark.Tests"
    //    |]
    //    |> Array.iter (fun x -> 
    //        DotNet.build (fun p ->
    //            { p with
    //                Configuration = buildConfiguration }) x
    //    )
                
    //}

    // Copies binaries from default VS location to expected bin folder
    // But keeps a subdirectory structure for each project in the
    // src folder to support multiple project outputs
    //Target.create "CopyBinaries" (fun _ ->
    //let binaries() =
    //    !! "**/src/**/*.??proj"
    //    -- "**/src/**/*.shproj"
    //    -- "**/src/**/Server.fsproj"
    //    |> Seq.map (fun f -> ((System.IO.Path.GetDirectoryName f) </> "bin" </> configuration, "bin" </> (System.IO.Path.GetFileNameWithoutExtension f)))
    //    |> Seq.filter (fun (fromDir, toDir) -> fromDir.ToLower().Contains("client") |> not)
    //    |> Seq.iter (fun (fromDir, toDir) -> Shell.copyDir toDir fromDir (fun _ -> true))


    //let copyBinaries = BuildTask.create "CopyBinaries" [build] {
    //    binaries()
    //}

    let theRun() =
        let jsPackageManager = getJsPackageManager ()
        
        let server = async {
            runDotNet "watch run" serverPath
        }
        let client = async {
            runTool jsPackageManager.RunTool "webpack-dev-server" __SOURCE_DIRECTORY__
        }
        let browser = async {
            do! Async.Sleep 5000
            openBrowser "http://localhost:8080"
        }

        let vsCodeSession = Environment.hasEnvironVar "vsCodeSession"
        let safeClientOnly = Environment.hasEnvironVar "safeClientOnly"

        let tasks =
            [ if not safeClientOnly then yield server
              yield client
              if not vsCodeSession then yield browser ]

        tasks
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

    let run = BuildTask.create "Run" [build; installClient] {
        theRun()
    }

    let runNoBuild = BuildTask.create "RunNoBuild" [installClient] {
        runDotNet "build" serverPath
        theRun()
    }

    BuildTask.createEmpty "All" [build; run]

let listAvailable() = BuildTask.listAvailable()
