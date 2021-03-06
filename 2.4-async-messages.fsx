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
    { emptyCh: Ch<unit>
      insCh: Ch<'a>
      remCh: Ch<'a>
      remAckCh: Ch<unit> }

[<AutoOpen>]
module Buffer =

    let create<'a> () : Buffer<'a> =
        let buf =
            { emptyCh = Ch<unit>()
              insCh = Ch<'a>()
              remCh = Ch<'a>()
              remAckCh = Ch<unit>() }

        let rec empty () =
            Ch.send buf.emptyCh () >>=.
            Ch.take buf.insCh ^=>
            full

        and full x :Job<unit> =
            Ch.send buf.remCh x >>=.
            Ch.take buf.remAckCh ^=>.
            empty ()

        start <| empty ()
        buf

    let insert buf v =
        Ch.take buf.emptyCh ^=>.
        Ch.send buf.insCh v

    let remove buf =
        job { let! v = Ch.take buf.remCh
              do! Ch.send buf.remAckCh ()
              return v }

let printResult i x = job { printfn "%i: got %i" i x }

let length = 1000
let workers = 4
// communication buffer
let buffer = create ()
// data to communicate
let data = [1..length]
// non-local consumers
let factory i = start <| foreverServer (remove buffer >>= printResult i)
let consumers = List.map factory [1..workers]
// local producer
run <| Seq.iterJobIgnore (insert buffer) data


