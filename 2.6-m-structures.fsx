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
    { empty : MVar<unit>
      full : MVar<'a> }

[<AutoOpen>]
module Buffer =
    let create (): Buffer<'a> =
        { empty = MVar(()); full = MVar() }

    let insert buf v =
        MVar.take buf.empty >>=. buf.full *<<= v

    let remove buf : Job<'a> =
        MVar.take buf.full >>= (fun x -> buf.empty *<<= () >>=. result x)

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


