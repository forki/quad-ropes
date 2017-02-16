﻿// Copyright (c) 2017 Florian Biermann, fbie@itu.dk

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:

// * The above copyright notice and this permission notice shall be
//   included in all copies or substantial portions of the Software.

// * The software is provided "as is", without warranty of any kind,
//   express or implied, including but not limited to the warranties of
//   merchantability, fitness for a particular purpose and
//   noninfringement. In no event shall the authors or copyright holders be
//   liable for any claim, damages or other liability, whether in an action
//   of contract, tort or otherwise, arising from, out of or in connection
//   with the software or the use or other dealings in the software.

module RadTrees.Benchmark

open CommandLine
open LambdaMicrobenchmarking

open RadTrees
open Types
open Examples


/// Benchmark a thunk with a message.
let benchmark (message, (thunk : unit -> 'a )) =
    Script.Of (message, System.Func<'a> thunk)


/// Add another thunk with a message to the script.
let (&>>) (script : 'a Script) (message, (thunk : unit -> 'a )) =
    script.Of (message, System.Func<'a> thunk)


/// Run the benchmark script.
let run (script : _ Script) =
    script.RunAll() |> ignore


/// Run the benchmark script and print header beforehand.
let runWithHead (script : _ Script) =
    script.WithHead() |> run


/// Options for command line.
type Options = {
  [<Option('m', "mode", Required = true, HelpText = "Which benchmark to run.")>] mode : string;
  [<Option('s', "size", Default = 100, HelpText = "Input size.")>] size : int;
  [<Option('t', "threads", Default = 1, HelpText = "Number of threads.")>] threads : int }


/// Benchmark base functions.
let all (opts : Options) =
    /// Benchmark sequential functions.
    let allSeq (opts : Options) =
        let rope = QuadRope.init opts.size opts.size (*)

        benchmark ("QuadRope.init", fun () -> QuadRope.init opts.size opts.size (*))
              &>> ("QuadRope.map",  fun () -> QuadRope.map (fun x -> x * x) rope)
              &>> ("QuadRope.zip",  fun () -> QuadRope.zip (+) rope rope) |> runWithHead
        benchmark ("QuadRope.reduce", (fun () -> QuadRope.reduce (+) 0 rope)) |> run

        let arr = Array2D.init opts.size opts.size (*)
        benchmark ("Array2D.init",    fun () -> Array2D.init opts.size opts.size (*))
              &>> ("Array2D.map",     fun () -> Array2D.map (fun x -> x * x) arr)
              &>> ("Array2D.zip",     fun () -> Array2D.map2 (+) arr arr) |> run
        benchmark ("Array2D.reduce",  fun () -> Array2D.reduce (+) arr) |> run


    /// Benchmark parallel functions.
    let allPar (opts : Options) =
        let rope = QuadRope.init opts.size opts.size (*)
        benchmark ("QuadRope.init",    fun () -> Parallel.QuadRope.init opts.size opts.size (*))
              &>> ("QuadRope.map",     fun () -> Parallel.QuadRope.map (fun x -> x * x) rope)
              &>> ("QuadRope.zip",     fun () -> Parallel.QuadRope.zip (+) rope rope) |> runWithHead
        benchmark ("QuadRope.reduce",  fun () -> Parallel.QuadRope.reduce (+) 0 rope) |> run

        let arr = Array2D.init opts.size opts.size (*)
        benchmark ("Array2D.init",    fun () -> Parallel.Array2D.init opts.size opts.size (*))
              &>> ("Array2D.map",     fun () -> Parallel.Array2D.map (fun x -> x * x) arr)
              &>> ("Array2D.zip",     fun () -> Parallel.Array2D.map2 (+) arr arr) |> run
        benchmark ("Array2D.reduce",  fun () -> Parallel.Array2D.reduce (+) arr) |> run


    // Don't run sequentially with task overhead.
    if opts.threads = 1 then
        allSeq opts
    else
        allPar opts


/// Benchmark van Der Corput sequence computation.
let vanDerCorput (opts : Options) =
    benchmark ("QuadRope.vdc", fun () -> VanDerCorput.QuadRope.vanDerCorput opts.size) |> runWithHead
    benchmark ("Array2D.vdc",  fun () -> VanDerCorput.Array2D.vanDerCorput opts.size) |> run


/// Benchmark matrix multiplication algorithms.
let matMult (opts: Options) =
    // Array matrices
    let arr1 = Array2D.init opts.size opts.size (fun i j -> float (i * j))
    let arr2 = Array2D.init opts.size opts.size (fun i j -> if i < j then 0.0 else 1.0)

    // Quad rope matrices
    let qr = QuadRope.init opts.size opts.size (fun i j -> float (i * j))
    let ud = QuadRope.init opts.size opts.size (fun i j -> if i < j then 0.0 else 1.0)
    let us = QuadRope.SparseDouble.upperDiagonal opts.size 1.0

    if opts.threads = 1 then
        benchmark ("QuadRope.mmult dense",  fun () -> MatMult.QuadRope.mmult qr ud)
              &>> ("QuadRope.mmult sparse", fun () -> MatMult.QuadRope.mmult qr us) |> runWithHead
        benchmark ("Array2D.mmult",      fun () -> MatMult.Array2D.mmult arr1 arr2)
              &>> ("Array2D.imperative", fun () -> MatMult.Imperative.mmult arr1 arr2) |> run
    else
        benchmark ("QuadRope.mmult dense",  fun () -> MatMult.QuadRope.mmultPar qr ud)
              &>> ("QuadRope.mmult sparse", fun () -> MatMult.QuadRope.mmultPar qr us) |> runWithHead
        benchmark ("Array2D.mmult",      fun () -> MatMult.Array2D.mmultPar arr1 arr2)
              &>> ("Array2D.imperative", fun () -> MatMult.Imperative.mmultPar arr1 arr2) |> run


/// Benchmark computing Fibonacci sequences.
let fibonacci (opts : Options) =
    benchmark ("QuadRope.fibonacci", fun () -> Fibonacci.QuadRope.fibseq opts.size) |> runWithHead
    benchmark ("Array2D.fibonacci", fun () -> Fibonacci.Array2D.fibseq opts.size) |> run


/// Benchmark integer factorization.
let factorize (opts : Options) =
    let qr = QuadRope.init opts.size opts.size (fun i j -> 1000 + i * j)
    let arr = Array2D.init opts.size opts.size (fun i j -> 1000 + i * j)
    if opts.threads = 1 then
        benchmark ("QuadRope.factorize", fun () -> Factorize.QuadRope.factorize qr) |> runWithHead
        benchmark ("Array2D.factorize",  fun () -> Factorize.Array2D.factorize arr) |> run
    else
        benchmark ("QuadRope.factorize", fun () -> Factorize.QuadRope.factorizePar qr) |> runWithHead
        benchmark ("Array2D.factorize",  fun () -> Factorize.Array2D.factorizePar arr) |> run


/// Benchmark the sieve of Erastothenes.
let sieve (opts : Options) =
    benchmark ("QuadRope.sieve", fun () -> Sieve.QuadRope.sieve opts.size) |> runWithHead
    benchmark ("Array2D.sieve",  fun () -> Sieve.Array2D.sieve opts.size) |> run


/// Benchmark index operations.
let index (opts : Options) =
    let qr = QuadRope.init opts.size opts.size (*)
    let arr = Array2D.init opts.size opts.size (*)
    let idx = opts.size / 2
    benchmark ("QuadRope.get", fun () -> QuadRope.get qr idx idx)
          &>> ("Array2D.get",  fun () -> Array2D.get arr idx idx) |> runWithHead


/// A map of benchmark functions and their names. If I had time, this
/// could be done with attributes instead.
let benchmarks =
    [ "all", all;
      "vdc", vanDerCorput;
      "mmult", matMult;
      "fibs", fibonacci;
      "primes", factorize;
      "sieve", sieve;
      "index", index ] |> Collections.Map


/// Set the number of thread pool threads to t.
let setThreads t =
    // Set min threads first, otherwise setting max threads might fail.
    System.Threading.ThreadPool.SetMinThreads(1, 1) |> ignore
    if not (System.Threading.ThreadPool.SetMaxThreads(t, t)) then
        failwith "# Error: could not change the number of thread pool threads."


/// True if we are running on mono.
let isMono = not (isNull (System.Type.GetType "Mono.Runtime"))


/// Run the benchmark that is required by command line args.
let runBenchmark (opts : Options) =
    if not isMono then
        setThreads opts.threads
    else if opts.threads <> 1 then
        printfn "# Warning: cannot change thread pool thread count on Mono."

    Script.WarmupIterations <- 3
    Script.Iterations <- 50
    (benchmarks.[opts.mode]) opts

[<EntryPoint>]
let main argv =
    match Parser.Default.ParseArguments<Options> argv with
        | :? Parsed<Options> as parsed -> runBenchmark parsed.Value; 0
        | _ -> 1