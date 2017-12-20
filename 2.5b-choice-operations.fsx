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
    { insCh : Ch<'a>
      remCh : Ch<'a> }

[<AutoOpen>]
module Buffer =
    let create size =
        let insCh = Ch<'a>()
        let remCh = Ch<'a>()

        let rec doRem buf =
            remCh *<- List.head buf
            ^=>. loop (List.tail buf)

        and doIns buf =
            Ch.take insCh
            ^=> fun x -> loop (buf @ [x])

        and loop = function
            | [] -> doIns []
            | buf ->
                if buf.Length > size then
                    doRem buf
                else
                    doRem buf <|> doIns buf

        loop [] |> start

        { insCh = insCh; remCh = remCh }

    let insert buf v =
        buf.insCh *<- v

    let remove buf =
        Ch.take buf.remCh

let printResult i x = job { printfn "%i: got %i" i x }

let length = 1000
let workers = 4
// communication buffer
let buffer = create 16
// data to communicate
let data = [1..length]
// non-local consumers
let factory i = remove buffer >>= printResult i |> foreverIgnore |> server
let consumers = List.map factory [1..workers]
// local producer
run <| timeOut (TimeSpan.FromSeconds 1.0)
run <| Seq.iterJobIgnore (insert buffer) data


