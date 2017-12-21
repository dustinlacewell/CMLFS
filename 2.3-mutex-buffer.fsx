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

[<Sealed>]
type Condition(m:MVar<unit>) =
    let mutex = m
    let mutable subs: IVar<unit> list = []

    member this.Wait () =
        let v = IVar<unit> ()
        // append v to subs
        subs <- (List.rev subs) |> fun x -> v :: x |> List.rev
        // release mutex, wait on condition, take mutex
        MVar.fill mutex () >>=. IVar.read v >>=. MVar.take mutex

    member this.Signal () =
        if subs.Length > 0 then
            start <| conIgnore [ for v in subs do yield IVar.fill v ()]
            subs <- []

[<AutoOpen>]
module Condition =
    let wait (c:Condition) = c.Wait ()
    let signal (c:Condition) = c.Signal ()

let withLock (m:MVar<unit>) (f: Unit -> Job<'b> ) =
    job { do! MVar.take m
          let! r = f ()
          do! MVar.fill m ()
          return r }

type Buffer<'a> =
    { mutable data: 'a option
      mu: MVar<unit>
      dataAvail: Condition
      dataEmpty: Condition }

[<AutoOpen>]
module Buffer =

    let create () =
        let mu = MVar(())
        { data = None
          mu = mu
          dataAvail = Condition (mu)
          dataEmpty = Condition (mu)}

    let internal write b (v:'a) = job { do b.data <- Some v }
    let internal clear b = job { do b.data <- None }

    let insert (buf:Buffer<int>) (v:int) =
        let rec waitLp data =
            match data with
            | None -> job {
                do! write buf v
                do  signal buf.dataAvail }
            | Some x -> job {
                do! wait buf.dataEmpty
                return! waitLp buf.data }

        withLock buf.mu (fun _ -> waitLp buf.data)

    let rec remove who (buf:Buffer<int>) =
        let rec waitLp = function
            | None -> job {
                do! wait buf.dataAvail
                return! waitLp buf.data }
            | Some v -> job {
                do buf.data <- None // clear buffer
                do signal buf.dataEmpty // signal empty condition
                return v} // return the removed value

        withLock buf.mu (fun _ -> waitLp buf.data)

let printResult i x = job { printfn "%i: got %i" i x }

let length = 1000
let workers = 4
// communication buffer
let buffer = create ()
// data to communicate
let data = [1..length]
// non-local consumers
let factory i = start <| foreverServer (remove (string i) buffer >>= printResult i)
let consumers = List.map factory [1..workers]
// local producer
run <| Seq.iterJobIgnore (insert buffer) data


