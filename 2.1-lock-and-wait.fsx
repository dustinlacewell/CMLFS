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
      lockSem: Semaphore
      waitSem: Semaphore }

let P = Semaphore.wait
let V = Semaphore.release

[<AutoOpen>]
module Buffer =

    let create () =
        { data = None
          waiting = 0
          lockSem = Semaphore(1)
          waitSem = Semaphore(0)}

    let internal write b (v:'a) = job { do b.data <- Some v }
    let internal clear b = job { do b.data <- None }
    let internal waitUp b = job { do b.waiting <- b.waiting + 1}
    let internal waitDown b = job { do b.waiting <- b.waiting - 1}

    let internal wait (buf:Buffer<'a>) =
        waitUp buf
        >>=. V buf.lockSem
        >>=. P buf.waitSem

    let rec internal signal (buf:Buffer<'a>) :Job<unit> =
        waitDown buf
        >>=. V buf.waitSem
        >>=. job { return! signal buf }
        |> whenDo (buf.waiting > 0)

    let rec insert (buf:Buffer<'a>) (v:'a) =
        P buf.lockSem
        >>=. delay (fun () ->
            match buf.data with
                | None ->
                        write buf v
                        >>=. signal buf
                        >>=. V buf.lockSem
                | Some _ ->
                        wait buf
                        >>=. insert buf v)

    let rec remove (buf:Buffer<'a>) =
        P buf.lockSem
        >>=. delay (fun () ->
            match (buf.data) with
                | Some v ->
                        clear buf
                        >>=. signal buf
                        >>=. V buf.lockSem
                        >>=. result v
                | None ->
                        wait buf
                        >>=. remove buf)

let printResult i x = job { do printfn "%A got %A" i x }

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


