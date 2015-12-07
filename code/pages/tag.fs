module FsSnip.Pages.Tag

open Suave
open Suave.Http
open Suave.Http.Applicatives
open System
open System.Web
open FsSnip.Utils
open FsSnip.Data
open FsSnip.Graphs
open XPlot.GoogleCharts

// -------------------------------------------------------------------------------------------------
// Tag page - domain model
// -------------------------------------------------------------------------------------------------

type TagLink = 
  { Text : string
    Link : string
    Size : int 
    Count : int }

type TagLinks = seq<TagLink>

type TagModel =
  { Tag : string
    Snippets : seq<Snippet> }

type AllTagsModel =
  { Taglinks: TagLinks
    Graph: Graph }

let getAllTags () = 
    let sorted = 
      publicSnippets
      |> Seq.collect (fun s -> s.Tags)
      |> Seq.countBy id
      |> Seq.sortBy (fun (_, c) -> -c)
      |> Seq.cache

    let links = 
      sorted
      |> Seq.withSizeBy snd
      |> Seq.map (fun ((n,c),s) -> 
          { Text = n; Size = 80 + s; Count = c;
            Link = HttpUtility.UrlEncode(n) })

    let image =
        [ (sorted |> Seq.take 10) ]
        |> Chart.Bar
        |> Chart.WithOptions (Options(title = "Top 10 tags"))
        |> Chart.WithLabels ["Count"]
        |> Chart.WithLegend true
        |> Chart.WithSize (600, 250)

    { Taglinks = links
      Graph = 
       { Id = image.Id
         Script = image.Js }}

// -------------------------------------------------------------------------------------------------
// Suave web parts
// -------------------------------------------------------------------------------------------------

// Loading tag page information (snippets by the given tag)
let showSnippets (tag) = 
    let t = System.Web.HttpUtility.UrlDecode tag
    let hasTag s = Seq.exists (fun t' -> t.Equals(t', StringComparison.InvariantCultureIgnoreCase)) s.Tags
    let ss = Seq.filter hasTag publicSnippets
    DotLiquid.page "tag.html" { Tag = t
                                Snippets = ss }

// Loading tag page information (all tags)
let showAll = delay (fun () -> 
  DotLiquid.page "tags.html" (getAllTags()))

// Composed web part to be included in the top-level route
let webPart = 
  choose
   [ path "/tags/" >>= showAll
     pathScan "/tags/%s" showSnippets ]
  