#r "System.Xml.Linq.dll"
#r "packages/Suave/lib/net40/Suave.dll"
#r "packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#r "packages/DotLiquid/lib/NET45/DotLiquid.dll"
#r "packages/Suave.DotLiquid/lib/net40/Suave.DotLiquid.dll"
#load "packages/FSharp.Formatting/FSharp.Formatting.fsx"
open System
open System.Web
open System.IO
open Suave
open Suave.Web
open Suave.Http
open Suave.Types
open FSharp.Data
open Suave.Http.Applicatives
open Suave.Http.Successful
open Suave.Http.Writers

// -------------------------------------------------------------------------------------------------
// Loading the FsSnip.WebSite project files
// -------------------------------------------------------------------------------------------------

#load "code/common/utils.fs"
#load "code/common/filters.fs"
#load "code/common/data.fs"
#load "code/common/rssfeed.fs"
#load "code/pages/home.fs"
#load "code/pages/insert.fs"
#load "code/pages/snippet.fs"
#load "code/pages/author.fs"
#load "code/pages/tag.fs"
#load "code/pages/rss.fs"
open FsSnip
open FsSnip.Data
open FsSnip.Utils
open FsSnip.Pages


// -------------------------------------------------------------------------------------------------
// Server entry-point and routing
// -------------------------------------------------------------------------------------------------

// TODO: This should be removed/fixed (see issue #4)
let browseStaticFile file ctx = async {
  let actualFile = Path.Combine(ctx.runtime.homeDirectory, "web", file)
  let mime = Suave.Http.Writers.defaultMimeTypesMap(Path.GetExtension(actualFile))
  let setMime =
    match mime with
    | None -> fun c -> async { return None }
    | Some mime -> Suave.Http.Writers.setMimeType mime.name
  return! ctx |> ( setMime >>= Successful.ok(File.ReadAllBytes actualFile) ) }

let browseStaticFiles ctx = async {
  let local = ctx.request.url.LocalPath
  let file = if local = "/" then "index.html" else local.Substring(1)
  return! browseStaticFile file ctx }

// Configure DotLiquid templates & register filters (in 'filters.fs')
[ for t in System.Reflection.Assembly.GetExecutingAssembly().GetTypes() do
    if t.Name = "Filters" && not (t.FullName.StartsWith "<") then yield t ]
|> Seq.last
|> DotLiquid.registerFiltersByType

DotLiquid.setTemplatesDir (__SOURCE_DIRECTORY__ + "/templates")

// Handles routing for the server
let app =
  choose
    [ path "/" >>= Home.showHome
      pathScan "/%s/%d" (fun (id, r) -> Snippet.showSnippet id (Revision r))
      pathWithId "/%s" (fun id -> Snippet.showSnippet id Latest)
      pathScan "/raw/%s/%d" (fun (id, r) -> Snippet.showRawSnippet id (Revision r))
      pathWithId "/raw/%s" (fun id -> Snippet.showRawSnippet id Latest)
      path "/pages/insert" >>= Insert.insertSnippet
      path "/authors/" >>= Author.showAll
      pathScan "/authors/%s" Author.showSnippets
      path "/tags/" >>= Tag.showAll
      pathScan "/tags/%s" Tag.showSnippets
      ( path "/rss/"
        <|> path "/rss"
        <|> path "/pages/Rss"
        <|> path "/pages/Rss/"
      ) >>= setHeader "Content-Type" "application/rss+xml; charset=utf-8" >>= Rss.getRss
      browseStaticFiles ]


// -------------------------------------------------------------------------------------------------
// To run the web site, you can use `build.sh` or `build.cmd` script, which is nice because it
// automatically reloads the script when it changes. But for debugging, you can also use run or
// run with debugger in VS or XS. This runs the code below.
// -------------------------------------------------------------------------------------------------

#if INTERACTIVE
#else
let cfg =
  { defaultConfig with
      bindings = [ HttpBinding.mk' HTTP  "127.0.0.1" 8011 ]
      homeFolder = Some __SOURCE_DIRECTORY__ }
let _, server = startWebServerAsync cfg app
Async.Start(server)
System.Diagnostics.Process.Start("http://localhost:8011")
System.Console.ReadLine() |> ignore
#endif
