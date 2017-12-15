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


let ps o = printfn "%A" o
let jps o = job { do ps o }

type Sem = MVar<unit>
let P sem = MVar.take sem
let V sem = MVar.fill sem ()

type Buffer<'a> =
    { mutable data: 'a option
      mutable waiting: int
      lockSem: MVar<unit>
      waitSem: MVar<unit> }

[<AutoOpen>]
module Buffer =

    let create () =
        { data = None
          waiting = 0
          lockSem = Sem (())
          waitSem = Sem  ()}

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

let buffer = create ()
let data = [0..99]

// non-local producer
start <| Seq.iterJobIgnore (fun x -> insert buffer x) data
// local consumer
run   <| forNIgnore (data.Length) (remove buffer >>= (fun x -> job { printfn "%i" x }))

