// Copyright (c) 2016 Florian Biermann, fbie@itu.dk

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

module RadTrees.Test.Parallel.QuadRope

open FsCheck
open RadTrees
open Types

type Handle = class end

let ``parallel init equal to sequential`` (NonNegativeInt h) (NonNegativeInt w) =
    (0 < h && 0 < w) ==> lazy (
        QuadRope.zip (=) (QuadRope.init h w (*)) (Parallel.QuadRope.init h w (*))
        |> QuadRope.reduce (&&))

let ``parallel hfold equal to sequential`` (a : int QuadRope) =
    let states = QuadRope.create (QuadRope.rows a) 1 1
    QuadRope.hfold (-) states a = Parallel.QuadRope.hfold (-) states a

let ``parallel vfold equal to sequential`` (a : int QuadRope) =
    let states = QuadRope.create 1 (QuadRope.cols a) 1
    QuadRope.vfold (-) states a = Parallel.QuadRope.vfold (-) states a

let sqr x = x * x

let ``parallel hmapreduce equal to sequential`` (a : int QuadRope) =
    QuadRope.hmapreduce sqr (*) a = Parallel.QuadRope.hmapreduce sqr (*) a

let ``parallel vmapreduce equal to sequential`` (a : int QuadRope) =
    QuadRope.vmapreduce sqr (*) a = Parallel.QuadRope.vmapreduce sqr (*) a

let ``parallel mapreduce equal to sequential``  (a : int QuadRope) =
    QuadRope.mapreduce sqr (*) a = Parallel.QuadRope.mapreduce sqr (*) a

let ``parallel map equal to sequential`` (a : int QuadRope) =
    QuadRope.map sqr a = Parallel.QuadRope.map sqr a

let ``parallel hrev equal to sequential`` (a : int QuadRope) =
    QuadRope.hrev a = Parallel.QuadRope.hrev a

let ``parallel vrev equal to sequential`` (a : int QuadRope) =
    QuadRope.vrev a = Parallel.QuadRope.vrev a

let ``parallel transpose equal to sequential`` (a : int QuadRope) =
    QuadRope.transpose a = Parallel.QuadRope.transpose a

let ``parallel zip equal to sequential`` (a : int QuadRope) (b : int QuadRope) =
    (QuadRope.rows a = QuadRope.rows b && QuadRope.cols a = QuadRope.cols b)
    ==> lazy (QuadRope.zip (+) a b = Parallel.QuadRope.zip (+) a b)
