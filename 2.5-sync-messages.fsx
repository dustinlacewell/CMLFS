#!/usr/bin/env fsharpi

#I "../../../.nuget/packages/hopac/0.3.21/lib/net45/"
#I "../../../.nuget/packages/hopac.extras/0.3.1/lib/net45"
#r "Hopac.Extras"
#r "Hopac.Platform"
#r "Hopac.Core"
#r "Hopac"

open System

open Hopac.Job
open Hopac
open Hopac.Infixes
open Hopac.Extensions


type Buffer<'a> =
    { insCh : Ch<'a>
      remCh : Ch<'a> }

[<AutoOpen>]
module Buffer =
    let create () =
        let insCh = Ch<'a>()
        let remCh = Ch<'a>()

        let rec empty () : Job<unit>=
            Ch.take insCh >>= full

        and full x : Alt<unit> =
            remCh *<- x ^=>. empty ()

        empty () |> start

        { insCh = insCh; remCh = remCh }

    let insert buf v =
        buf.insCh *<- v

    let remove buf =
        Ch.take buf.remCh

let printResult i x = job { printfn "%i: got %i" i x }

let length = 1000
let workers = 4
// communication buffer
let buffer = create ()
// data to communicate
let data = [1..length]
// non-local consumers
let factory i = remove buffer >>= printResult i |> foreverIgnore |> server
let consumers = List.map factory [1..workers]
// local producer
run <| Seq.iterJobIgnore (insert buffer) data


