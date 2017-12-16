#!/usr/bin/env fsharpi

#I "../../../.nuget/packages/hopac/0.3.21/lib/net45/"
#r "Hopac.Platform"
#r "Hopac.Core"
#r "Hopac"

open System.Collections.Generic

open Hopac.Job
open Hopac
open Hopac.Infixes
open Hopac.Extensions

type Sem = MVar<unit>
let P sem = MVar.take sem
let V sem = MVar.fill sem ()

type Buffer<'a> =
    { mutable data: 'a option
      mutable waiting: int
      emptySem: MVar<unit>
      fullSem: MVar<unit> }

[<AutoOpen>]
module Buffer =

    let create () =
        { data = None
          waiting = 0
          emptySem = Sem (())
          fullSem = Sem  ()}

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

let example length workers =
    // communication buffer
    let buffer = create ()
    // data to communicate
    let data = [1..length]
    // local consumer
    let factory i = start <| foreverServer (remove buffer >>= printResult i)
    let consumers = List.map factory [1..workers]
    // non-local producer
    run <| Seq.iterJobIgnore (insert buffer) data


