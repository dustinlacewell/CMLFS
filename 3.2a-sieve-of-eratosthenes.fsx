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


let counter (i:int) =
    let ch = Ch()
    let rec loop x = job { return! ch *<- x ^=>. loop (x + 1) }
    loop i |> server
    ch

let filter (p:int) (inCh:Ch<int>) =
    let outCh = Ch()
    let rec loop () =
        Ch.take inCh >>= fun x ->
            if x % p <> 0 then
                outCh *<- x >>=. loop ()
            else
                loop ()

    loop () |> server
    outCh

let sieve () =
    let primes = Ch()
    let rec head (ch:Ch<int>) =
        Ch.take ch >>= fun x ->
            primes *<- x >>=. head (filter x ch)

    start (head (counter 2))
    primes


let printResult x = job { printfn "%i" x }

let length = 1000
// communication buffer
let primes = sieve ()
run <| Job.forNIgnore length (Ch.take primes >>= printResult)


