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


type Request<'a> = GET | PUT of 'a

type Cell<'a> =
    { reqCh: Ch<Request<'a>>
      replyCh : Ch<'a> }


[<AutoOpen>]
module Cell =
    let create x =
        let reqCh = Ch()
        let replyCh = Ch()
        let rec loop x =
            Ch.take reqCh >>= delayWith (function
                | GET -> replyCh *<- x >>=. loop x
                | PUT v -> loop v)
        start (loop x)

        { reqCh = reqCh; replyCh = replyCh }

    let get c = c.reqCh *<- GET ^=>. Ch.take c.replyCh

    let put c x = c.reqCh *<- PUT x

let printResult i x = job { printfn "%i: got %i" i x }

let length = 1000
let workers = 4
// communication buffer
let cell = create 0
// data to communicate
let data = [1..length]
// non-local consumers
let factory i = get cell >>= printResult i |> foreverIgnore |> server
let consumers = List.map factory [1..workers]
// local producer
run <| Seq.iterJobIgnore (put cell) data


