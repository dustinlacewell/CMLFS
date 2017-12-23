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


let rec loop f s = f s >>= delayWith (loop f)

let forever (init:'a) (f:'a->Job<'a>) =
    start (loop f init)

let add inCh1 inCh2 outCh =
    forever () (fun () -> job {
        let! a = Ch.take inCh1
        let! b = Ch.take inCh2
        return! outCh *<- (a + b) })

let delay init (inCh, outCh) =
    let none () =
        Ch.take inCh ^=>
        delayWith (fun x -> Job.result (Some x))

    let some x =
        outCh *<- x ^=>.
        delay (fun _ -> Job.result None)

    forever init (function None -> upcast (none ()) | Some x -> upcast (some x))

let copy inCh outCh1 outCh2 =
    let send x =
        outCh1 *<- x ^=>.
        outCh2 *<- x

    let take () =
        Ch.take inCh >>=
        send

    forever () take

let mkFibNetwork () =
    let outCh = Ch()
    let (c1, c2, c3, c4, c5) = (Ch(), Ch(), Ch(), Ch(), Ch())
    let start = 0I
    delay (Some start) (c4, c5)
    copy c2 c3 c4
    add c3 c5 c1
    copy c1 c2 outCh
    run (c1 *<- 1I)
    outCh

let printResult x = job { printfn "%A" x }

let length = 1000
// communication buffer
let fib = mkFibNetwork ()
run <| Job.forNIgnore length (Ch.take fib >>= printResult)


