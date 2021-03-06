#!/usr/bin/env fsharpi

#I "../../../.nuget/packages/hopac/0.3.21/lib/net45/"
#I "../../../.nuget/packages/hopac.extras/0.3.1/lib/net45"
#r "Hopac.Extras"
#r "Hopac.Platform"
#r "Hopac.Core"
#r "Hopac"

open System.Collections.Generic

open Hopac.Job
open Hopac
open Hopac.Infixes
open Hopac.Extensions
open Hopac.Extras

type Buffer<'a> =
    { mutable data: 'a option
      mutable waiting: int
      emptySem: Semaphore
      fullSem: Semaphore }

let P = Semaphore.wait
let V = Semaphore.release

[<AutoOpen>]
module Buffer =

    let create () =
        { data = None
          waiting = 0
          emptySem = Semaphore(1)
          fullSem = Semaphore(0)}

    let internal write b (v:'a) = job { do b.data <- Some v }
    let internal clear b = job { do b.data <- None }

    let insert (buf:Buffer<'a>) (v:'a) =
        P buf.emptySem
        >>=. write buf v
        >>=. V buf.fullSem

    let rec remove (buf:Buffer<'a>) =
        P buf.fullSem >>=.
        delay (fun () ->
            match buf.data with
                | Some v ->
                  V buf.emptySem
                  >>=. result v
                | None ->
                    V buf.fullSem
                    >>=. remove buf)

let printResult i x = job { printfn "%i got %i" i x }

let length = 1000
let workers = 4
// communication buffer
let buffer = create ()
// data to communicate
let data = [1..length]
// local consumer
let factory i = start <| foreverServer (remove buffer >>= printResult i)
let consumers = List.map factory [1..workers]
// non-local producer
run <| Seq.iterJobIgnore (insert buffer) data
