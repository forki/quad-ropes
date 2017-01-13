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

[<RequireQualifiedAccessAttribute>]
[<CompilationRepresentationAttribute(CompilationRepresentationFlags.ModuleSuffix)>]
module RadTrees.Parallel.QuadRope

open RadTrees
open Types
open Utils.Tasks

let private rows = Types.rows
let private cols = Types.cols
let private depth = Types.depth
let private isEmpty = Types.isEmpty

let private isSparse = QuadRope.isSparse
let private node = QuadRope.node
let private leaf = QuadRope.leaf
let private flatNode = QuadRope.flatNode
let private thinNode = QuadRope.thinNode

let private s_max = QuadRope.s_max

// These come in handy for making the code more concise; not
// necessarily more readable though.
let (||||>) (a, b, c, d) f =
    f a b c d

/// Generate a new quad rope in parallel.
let init h w (f : int -> int -> _) =
    let rec init slc =
        if ArraySlice.rows slc <= 0 || ArraySlice.cols slc <= 0 then
            Empty
        else if ArraySlice.rows slc <= s_max && ArraySlice.cols slc <= s_max then
            ArraySlice.init slc f // Write into shared array.
            leaf slc
        else if ArraySlice.cols slc <= s_max then
            par2 (fun () -> init (ArraySlice.upperHalf slc)) (fun () -> init (ArraySlice.lowerHalf slc))
            ||> thinNode
        else if ArraySlice.rows slc <= s_max then
            par2 (fun () -> init (ArraySlice.leftHalf slc)) (fun () -> init (ArraySlice.rightHalf slc))
            ||> flatNode
        else
            par4 (fun () -> init (ArraySlice.ne slc))
                 (fun () -> init (ArraySlice.nw slc))
                 (fun () -> init (ArraySlice.sw slc))
                 (fun () -> init (ArraySlice.se slc))
            ||||> node
    init (ArraySlice.zeroCreate h w)

/// Apply a function f to all scalars in parallel.
let map f qr =
    let rec map qr tgt =
        match qr with
            | Empty -> Empty

            // Initialize target as soon as quad rope is dense.
            | _ when not (isSparse qr) && Target.isEmpty tgt ->
                map qr (Target.make (rows qr) (cols qr))

            // Write into target and construct new leaf.
            | Leaf slc ->
                leaf (Target.map f slc tgt)

            // Node recursive, parallel cases, adjust target descriptor.
            | Node (_, _, _, _, ne, nw, Empty, Empty) ->
                par2 (fun () -> map nw tgt) (fun () -> map ne (Target.ne tgt qr)) ||> flatNode

            | Node (_, _, _, _, Empty, nw, sw, Empty) ->
                par2 (fun () -> map nw tgt) (fun () -> map sw (Target.sw tgt qr)) ||> thinNode

            | Node (_, _, _, _, ne, nw, sw, se) ->
                par4 (fun () -> map ne (Target.ne tgt qr))
                     (fun () -> map nw tgt)
                     (fun () -> map sw (Target.sw tgt qr))
                     (fun () -> map se (Target.se tgt qr))
                ||||> node

            // Materialize quad rope and then map.
            | Slice _ ->
                map (QuadRope.materialize qr) tgt

            // The target is empty, don't write.
            | Sparse (h, w, v) ->
                Sparse (h, w, f v)

    map qr Target.empty

/// Map a function f to each row of the quad rope.
let hmap f qr =
    init (rows qr) 1 (fun i _ -> QuadRope.slice i 0 1 (cols qr) qr |> f)

/// Map a function f to each column of the quad rope.
let vmap f qr =
    init 1 (cols qr) (fun _ j -> QuadRope.slice 0 j (rows qr) 1 qr |> f)

/// Zip implementation for the general case where we do not assume
/// that both ropes have the same internal structure.
let rec private genZip f lqr rqr tgt =
    match lqr with
        // Initialize target if any of the two quad ropes is dense.
        | _ when Target.isEmpty tgt && not (isSparse lqr) ->
            genZip f lqr rqr (Target.make (rows lqr) (cols lqr))
        | Node (_, _, _, _, ne, nw, Empty, Empty) ->
            par2 (fun () ->
                  let rnw = QuadRope.slice 0 0 (rows nw) (cols nw) rqr
                  genZip f nw rnw tgt)
                 (fun () ->
                  let rne = QuadRope.slice 0 (cols nw) (rows ne) (cols ne) rqr
                  genZip f ne rne (Target.ne tgt lqr))
            ||> flatNode

        | Node (_, _, _, _, Empty, nw, sw, Empty) ->
            par2 (fun () ->
                  let rnw = QuadRope.slice 0 0 (rows nw) (cols nw) rqr
                  genZip f nw rnw tgt)
                 (fun () ->
                  let rsw = QuadRope.slice (rows nw)  0 (rows sw) (cols sw) rqr
                  genZip f sw rsw (Target.sw tgt lqr))
            ||> thinNode

        | Node (_, _, _, _, ne, nw, sw, se) ->
            par4 (fun () ->
                  let rne = QuadRope.slice 0 (cols nw) (rows ne) (cols ne) rqr
                  genZip f ne rne (Target.ne tgt lqr))
                 (fun () ->
                  let rnw = QuadRope.slice 0 0 (rows nw) (cols nw) rqr
                  genZip f nw rnw tgt)
                 (fun () ->
                  let rsw = QuadRope.slice (rows nw) 0 (rows sw) (cols sw) rqr
                  genZip f sw rsw (Target.sw tgt lqr))
                 (fun () ->
                  let rse = QuadRope.slice (rows ne) (cols sw) (rows se) (cols se) rqr
                  genZip f se rse (Target.se tgt lqr))
            ||||> node

        | Slice _ -> genZip f (QuadRope.materialize lqr) rqr tgt
        | _ -> QuadRope.genZip f lqr rqr tgt

/// Zip function that assumes that the internal structure of two ropes
/// matches. If not, it falls back to the slow, general case.
let rec private fastZip f lqr rqr tgt =
    match lqr, rqr with
        | Empty, Empty -> Empty

        // Initialize target if any of the two quad ropes is dense.
        | _ when Target.isEmpty tgt && (not (isSparse lqr) || not (isSparse rqr)) ->
            fastZip f lqr rqr (Target.make (rows lqr) (cols lqr))

        | Leaf _, Leaf _ when QuadRope.shapesMatch lqr rqr ->
                QuadRope.fastZip f lqr rqr tgt

        | Node (_, _, _, _, lne, lnw, Empty, Empty), Node (_, _, _, _, rne, rnw, Empty, Empty)
            when QuadRope.subShapesMatch lqr rqr ->
                par2 (fun () -> fastZip f lnw rnw tgt)
                     (fun () -> fastZip f lne rne (Target.ne tgt lqr))
                ||> flatNode

        | Node (_, _, _, _, Empty, lnw, lsw, Empty), Node (_, _, _, _, Empty, rnw, rsw, Empty)
            when QuadRope.subShapesMatch lqr rqr ->
                par2 (fun () -> fastZip f lnw rnw tgt)
                     (fun () -> fastZip f lsw rsw (Target.sw tgt lqr))
                ||> thinNode

        | Node (_, _, _, _, lne, lnw, lsw, lse), Node (_, _, _, _, rne, rnw, rsw, rse)
            when QuadRope.subShapesMatch lqr rqr ->
                par4 (fun () -> fastZip f lne rne (Target.ne tgt lqr))
                     (fun () -> fastZip f lnw rnw tgt)
                     (fun () -> fastZip f lsw rsw (Target.sw tgt lqr))
                     (fun () -> fastZip f lse rse (Target.se tgt lqr))
                ||||> node

        // It may pay off to reallocate first if both reallocated quad
        // ropes have the same internal shape. This might be
        // over-fitted to matrix multiplication.
        | Slice _, Slice _ -> fastZip f (QuadRope.materialize lqr) (QuadRope.materialize rqr) tgt
        | Slice _, _ ->       fastZip f (QuadRope.materialize lqr) rqr tgt
        | Slice _, Slice _ -> fastZip f lqr (QuadRope.materialize rqr) tgt

        // Sparse branches can be reduced to either a single call to f
        // in case both arguments are sparse, or to a mapping f.
        | Sparse (h, w, v1), Sparse (_, _, v2) -> Sparse (h, w, f v1 v2)
        | Sparse (_, _, v), _ -> map (f v) rqr
        | _, Sparse (_, _, v) -> map (fun x -> f x v) lqr

        // Fall back to general case.
        | _ -> genZip f lqr rqr tgt

/// Apply f to each (i, j) of lqr and rqr.
let zip f lqr rqr =
    if not (QuadRope.shapesMatch lqr rqr) then
        invalidArg "rqr" "Must have the same shape as first argument."
    fastZip f lqr rqr (Target.make (rows lqr) (cols lqr))

/// Apply f to all values of the rope and reduce the resulting values
/// to a single scalar using g in parallel. Variable epsilon is the
/// neutral element for g. We assume that g epsilon x = g x epsilon =
/// x.
let rec mapreduce f g epsilon = function
    | Node (_, _, _, _, ne, nw, Empty, Empty) ->
        par2 (fun () -> mapreduce f g epsilon nw) (fun () -> mapreduce f g epsilon ne) ||> g

    | Node (_, _, _, _, Empty, nw, sw, Empty) ->
        par2 (fun () -> mapreduce f g epsilon nw) (fun () -> mapreduce f g epsilon sw) ||> g

    | Node (_, _, _, _, ne, nw, sw, se) ->
        let ne', nw', sw', se' = par4 (fun () -> mapreduce f g epsilon ne)
                                      (fun () -> mapreduce f g epsilon nw)
                                      (fun () -> mapreduce f g epsilon sw)
                                      (fun () -> mapreduce f g epsilon se)
        g (g nw' ne') (g sw' se')
    | Slice _ as qr -> mapreduce f g epsilon (QuadRope.materialize qr)
    | qr -> QuadRope.mapreduce f g epsilon qr

/// Reduce all values of the rope to a single scalar in parallel.
let reduce f epsilon qr = mapreduce id f epsilon qr

let hmapreduce f g epsilon qr = hmap (mapreduce f g epsilon) qr
let vmapreduce f g epsilon qr = vmap (mapreduce f g epsilon) qr

let hreduce f epsilon qr = hmapreduce id f epsilon qr
let vreduce f epsilon qr = vmapreduce id f epsilon qr

/// Apply predicate p to all elements of qr in parallel and reduce the
/// elements in both dimension using logical and.
let forall p qr = mapreduce p (&&) true qr

/// Apply predicate p to all elements of qr in parallel and reduce the
/// elements in both dimensions using logical or.
let exists p qr = mapreduce p (||) false qr


/// Remove all elements from rope for which p does not hold in
/// parallel. Input rope must be of height 1.
let rec hfilter p = function
    | Node (_, _, 1, _, ne, nw, Empty, Empty) ->
        par2 (fun () -> hfilter p nw) (fun () -> hfilter p ne) ||> flatNode
    | Slice _ as qr -> hfilter p (QuadRope.materialize qr)
    | qr -> QuadRope.hfilter p qr

/// Remove all elements from rope for which p does not hold in
/// parallel. Input rope must be of width 1.
let rec vfilter p = function
    | Node (_, _, _, 1, Empty, nw, sw, Empty) ->
        par2 (fun () -> vfilter p nw) (fun () -> vfilter p sw) ||> thinNode
    | Slice _ as qr -> vfilter p (QuadRope.materialize qr)
    | qr -> QuadRope.vfilter p qr

/// Reverse the quad rope horizontally in parallel.
let hrev qr =
    let rec hrev qr tgt =
        match qr with
            | Leaf slc ->
                leaf (Target.hrev slc tgt)
            | Node (_, _, _, _, ne, nw, Empty, Empty) ->
                par2 (fun () -> hrev ne (Target.ne tgt qr)) (fun () -> hrev nw tgt) ||> flatNode

            | Node (_, _, _, _, Empty, nw, sw, Empty) ->
                par2 (fun () -> hrev nw tgt) (fun () -> hrev sw (Target.sw tgt qr)) ||> thinNode

            | Node (_, _, _, _, ne, nw, sw, se) ->
                par4 (fun () -> hrev nw tgt)
                     (fun () -> hrev ne (Target.ne tgt qr))
                     (fun () -> hrev se (Target.se tgt qr))
                     (fun () -> hrev sw (Target.sw tgt qr))
                ||||> node
            | Slice _ ->
                hrev (QuadRope.materialize qr) tgt
            | _ -> qr
    hrev qr (Target.make (rows qr) (cols qr))

/// Reverse the quad rope vertically in parallel.
let vrev qr =
    let rec vrev qr tgt =
        match qr with
            | Leaf slc ->
                leaf (Target.vrev slc tgt)
            | Node (_, _, _, _, ne, nw, Empty, Empty) ->
                par2 (fun () -> vrev nw tgt) (fun () -> vrev ne (Target.ne tgt qr)) ||> flatNode

            | Node (_, _, _, _, Empty, nw, sw, Empty) ->
                par2 (fun () -> vrev sw (Target.sw tgt qr)) (fun () -> vrev nw tgt) ||> thinNode

            | Node (_, _, _, _, ne, nw, sw, se) ->
                par4 (fun () -> vrev se (Target.se tgt qr))
                     (fun () -> vrev sw (Target.sw tgt qr))
                     (fun () -> vrev nw tgt)
                     (fun () -> vrev ne (Target.ne tgt qr))
                ||||> node

            | Slice _ ->
                vrev (QuadRope.materialize qr) tgt
            | _ -> qr
    vrev qr (Target.make (rows qr) (cols qr))

/// Transpose the quad rope in parallel. This is equal to swapping
/// indices, such that get rope i j = get rope j i.
let transpose qr =
    let rec transpose qr tgt =
        match qr with
            | Empty -> Empty
            | Leaf slc ->
                leaf (Target.transpose slc tgt)
            | Node (_, _, _, _, ne, nw, Empty, Empty) ->
                par2 (fun () -> transpose nw tgt) (fun () -> transpose ne (Target.ne tgt qr)) ||> thinNode

            | Node (_, _, _, _, Empty, nw, sw, Empty) ->
                par2 (fun () -> transpose nw tgt) (fun () -> transpose sw (Target.sw tgt qr)) ||> flatNode

            | Node (_, _, _, _, ne, nw, sw, se) ->
                par4 (fun () -> transpose sw (Target.sw tgt qr))
                     (fun () -> transpose nw tgt)
                     (fun () -> transpose ne (Target.ne tgt qr))
                     (fun () -> transpose se (Target.se tgt qr))
                ||||> node
            | Slice _ ->
                transpose (QuadRope.materialize qr) tgt
            | Sparse (h, w, v) -> Sparse (w, h, v)
    transpose qr (Target.make (cols qr) (rows qr))

/// Apply a function with side effects to all elements and their
/// corresponding index pair in parallel.
let iteri f qr =
    let rec iteri f i j = function
        | Empty -> ()
        | Leaf vs -> ArraySlice.iteri (fun i0 j0 v -> f (i + i0) (j + j0) v) vs

        | Node (_, _, _, _, ne, nw, Empty, Empty) ->
            par2 (fun () -> iteri f i j nw) (fun () -> iteri f i (j + cols nw) ne)
            |> ignore

        | Node (_, _, _, _, Empty, nw, sw, Empty) ->
            par2 (fun () -> iteri f i j nw) (fun () -> iteri f (i + rows nw) j sw)
            |> ignore

        | Node (_, _, _, _, ne, nw, sw, se) ->
            par4 (fun () -> iteri f i j nw)
                 (fun () -> iteri f i (j + cols nw) ne)
                 (fun () -> iteri f (i + rows nw) j sw)
                 (fun () -> iteri f (i + rows ne) (j + cols sw) se)
            |> ignore

        | Slice _ as qr -> iteri f i j (QuadRope.materialize qr)

        // Small sparse quad rope, process sequentially.
        | Sparse (h, w, v) when h <= s_max && w <= s_max ->
            for r in i .. i + h - 1 do
                for c in j .. j + w - 1 do
                    f r c v

        // Wide sparse quad rope, split horizontally.
        | Sparse (h, w, _) as qr when h <= s_max ->
            let nw, ne = QuadRope.hsplit2 qr (w / 2)
            par2 (fun () -> iteri f i j nw) (fun () -> iteri f i (j + w / 2) ne) |> ignore

        // Tall sparse quad rope, split vertically.
        | Sparse (h, w, _) as qr when w <= s_max ->
            let nw, sw = QuadRope.vsplit2 qr (h / 2)
            par2 (fun () -> iteri f i j nw) (fun () -> iteri f (i + h / 2) j sw) |> ignore

        | Sparse _ as qr ->
            let ne, nw, sw, se = QuadRope.split4 qr
            par4 (fun () -> iteri f i (j + cols nw) ne)
                 (fun () -> iteri f i j nw)
                 (fun () -> iteri f (i + rows nw) j sw)
                 (fun () -> iteri f (i + rows ne) (j + cols sw) se)
            |> ignore

    iteri f 0 0 qr

/// Conversion into 2D array.
let toArray2D qr =
    let arr = Array2D.zeroCreate (rows qr) (cols qr)
    // This avoids repeated calls to get; it is enough to traverse
    // the rope once.
    iteri (fun i j v -> arr.[i, j] <- v) qr
    arr

/// Initialize a rope from a native 2D-array in parallel.
let fromArray2D arr =
    let rec init h0 w0 h1 w1 arr =
        let h = h1 - h0
        let w = w1 - w0
        if h <= QuadRope.s_max && w <= QuadRope.s_max then
            QuadRope.leaf (ArraySlice.makeSlice h0 w0 h w arr)
        else if w <= QuadRope.s_max then
            let hpv = h0 + (h >>> 1)
            let n, s = par2 (fun () -> init h0 w0 hpv w1 arr) (fun () -> init hpv w0 h1 w1 arr)
            QuadRope.thinNode n s
        else if h <= QuadRope.s_max then
            let wpv = w0 + (w >>> 1)
            let w, e = par2 (fun () -> init h0 w0 h1 wpv arr) (fun () -> init h0 wpv h1 w1 arr)
            QuadRope.flatNode w e
        else
            let hpv = h0 + (h >>> 1)
            let wpv = w0 + (w >>> 1)
            let ne, nw, sw, se = par4 (fun () -> init h0 wpv hpv w1 arr)
                                      (fun () -> init h0 w0 hpv wpv arr)
                                      (fun () -> init hpv w0 h1 wpv arr)
                                      (fun () -> init hpv wpv h1 w1 arr)
            QuadRope.node ne nw sw se
    init 0 0 (Array2D.length1 arr) (Array2D.length2 arr) arr

/// Reallocate a rope form the ground up. Sometimes, this is the
/// only way to improve performance of a badly composed quad rope.
let inline reallocate qr =
    fromArray2D (toArray2D qr)

/// Initialize a rope in parallel where all elements are
/// <code>e</code>.
let initAll h w e =
    init h w (fun _ _ -> e)

/// Initialize a rope in parallel with all zeros.
let initZeros h w = initAll h w 0

/// Compute the generalized summed area table for functions plus and
/// minus in parallel, if possible; all rows and columns are
/// initialized with init.
let scan plus minus init qr =
    // Prefix is implicitly passed on through side effects when
    // writing into tgt. Therefore, we need to take several cases of
    // dependencies into account:
    //
    // - flat or thin node: no parallelism.
    // - rows ne <= rows nw && cols sw <= cols nw: scan ne and sw in parallel.
    // - cols nw < cols sw: scan ne before sw.
    // - rows nw < rows ne: scan sw before ne.
    let rec scan pre qr tgt =
        match qr with
            | Empty -> Empty
            | Leaf slc ->
                Target.scan plus minus pre slc tgt
                Leaf (Target.toSlice tgt (ArraySlice.rows slc) (ArraySlice.cols slc))

            // Flat node, no obvious parallelism.
            | Node (s, d, h, w, ne, nw, Empty, Empty) ->
                let nw' = scan pre nw tgt
                let tgt_ne = Target.ne tgt qr
                let ne' = scan (Target.get tgt_ne) ne tgt_ne
                Node (s, d, h, w, ne', nw', Empty, Empty)

            // Thin nodes, no obvious parallelism.
            | Node (s, d, h, w, Empty, nw, sw, Empty) ->
                let nw' = scan pre nw tgt
                let tgt_sw = Target.sw tgt qr
                let sw' = scan (Target.get tgt_sw) sw tgt_sw
                Node (s, d, h, w, Empty, nw', sw', Empty)

            // Parallel case; NE and SW depend only on NW, SE
            // depends on all other children. Scan NE and SW in
            // parallel.
            | Node (s, d, h, w, ne, nw, sw, se) when rows ne <= rows nw && cols sw <= cols nw ->

                let nw' = scan pre nw tgt
                let ne', sw' = par2 (fun () ->
                                     let tgt_ne = Target.ne tgt qr
                                     scan (Target.get tgt_ne) ne tgt_ne)
                                    (fun () ->
                                     let tgt_sw = Target.sw tgt qr
                                     scan (Target.get tgt_sw) sw tgt_sw)
                let tgt_se = Target.se tgt qr
                let se' = scan (Target.get tgt_se) se tgt_se
                Node (s, d, h, w, ne', nw', sw', se')

            // Sequential case; SW depends on NE.
            | Node (s, d, h, w, ne, nw, sw, se) when cols nw < cols sw ->
                let nw' = scan pre nw tgt
                let tgt_ne = Target.ne tgt qr
                let ne' = scan (Target.get tgt_ne) ne tgt_ne
                let tgt_sw = Target.sw tgt qr
                let sw' = scan (Target.get tgt_sw) sw tgt_sw
                let tgt_se = Target.se tgt qr
                let se' = scan (Target.get tgt_se) se tgt_se
                Node (s, d, h, w, ne', nw', sw', se')

            // Sequential case; NE depends on SW.
            | Node (s, d, h, w, ne, nw, sw, se) -> // when rows nw < rows ne
                let nw' = scan pre nw tgt
                let tgt_sw = Target.sw tgt qr
                let sw' = scan (Target.get tgt_sw) sw tgt_sw
                let tgt_ne = Target.ne tgt qr
                let ne' = scan (Target.get tgt_ne) ne tgt_ne
                let tgt_se = Target.se tgt qr
                let se' = scan (Target.get tgt_se) se tgt_se
                Node (s, d, h, w, ne', nw', sw', se')

            | Slice _ -> scan pre (QuadRope.materialize qr) tgt

            // Materialize a sparse leaf and scan it.
            | Sparse (h, w, v) when h <= s_max || w <= s_max ->
                // Fill the target imperatively.
                Target.fill tgt h w v
                // Get a slice over the target.
                let slc = Target.toSlice tgt h w
                // Compute prefix imperatively.
                Target.scan plus minus pre slc tgt
                // Make a new quad rope from array slice.
                leaf slc

            // If both dimensions are larger than s_max, we can
            // actually do some work in parallel.
            | Sparse _ -> // when s_max < h && s_max < w
                let ne, nw, sw, se = QuadRope.split4 qr
                let nw' = scan pre nw tgt
                let ne', sw' = par2 (fun () ->
                                     let tgt_ne = Target.incrementCol tgt (cols nw')
                                     scan (Target.get tgt_ne) ne tgt_ne)
                                    (fun () ->
                                     let tgt_sw = Target.incrementRow tgt (rows nw')
                                     scan (Target.get tgt_sw) sw tgt_sw)
                let tgt_se = Target.increment tgt (rows nw') (cols sw')
                let se' = scan (Target.get tgt_se) se tgt_se
                node ne' nw' sw' se'

    // Initial target and start of recursion.
    let tgt = Target.makeWithFringe (rows qr) (cols qr) init
    scan (Target.get tgt) qr tgt

/// Compare two quad ropes element wise and return true if they are
/// equal. False otherwise.
let equals qr0 qr1 =
    reduce (&&) true (zip (=) qr0 qr1)

module SparseDouble =
    /// We cannot optimize any further than parallelizing; reduce is
    /// already optimized for sparse quad ropes.
    let sum = reduce (+) 0.0

    /// Compute the product of all values in a quad rope in
    /// parallel. This function short-circuits the computation where
    /// possible, but it does not stop already running computations.
    let rec prod = function
        | Node (_, _, _, _, Sparse (_, _, 0.0), _, _, _)
        | Node (_, _, _, _, _, Sparse (_, _, 0.0), _, _)
        | Node (_, _, _, _, _, _, Sparse (_, _, 0.0), _)
        | Node (_, _, _, _, _, _, _, Sparse (_, _, 0.0))
        | Sparse (_, _, 0.0) -> 0.0
        | Sparse (_, _, 1.0) -> 1.0
        | Node (true, _, _, _, ne, nw, Empty, Empty) ->
            par2 (fun () -> prod ne) (fun () -> prod nw) ||> (*)
        | Node (true, _, _, _, Empty, nw, sw, Empty) ->
            par2 (fun () -> prod nw) (fun () -> prod sw) ||> (*)
        | Node (true, _, _, _, ne, nw, sw, se) ->
            let n = par2 (fun () -> prod ne) (fun () -> prod nw) ||> (*)
            if n = 0.0 then
                0.0
            else
                n * (par2 (fun () -> prod sw) (fun () -> prod se) ||> (*))

        | Slice _ as qr -> prod (QuadRope.materialize qr)
        | qr -> reduce (*) 1.0 qr

    /// Multiply two quad ropes of doubles point-wise.
    let pointwise lqr rqr =
        /// General case for quad ropes of different structure.
        let rec gen lqr rqr =
            match rqr with
                // Sparse quad ropes of zero result in zero.
                | Sparse (_, _, 0.0) -> rqr
                | Sparse (_, _, 1.0) -> lqr
                | _ ->
                    match lqr with
                        // Flat node.
                        | Node (true, _, _, _, ne, nw, Empty, Empty) ->
                                let ne', nw' = par2 (fun () -> gen ne (QuadRope.sliceToMatchNE lqr rqr))
                                                    (fun () -> gen nw (QuadRope.sliceToMatchNW lqr rqr))
                                flatNode nw' ne'

                        // Thin node.
                        | Node (true, _, _, _, Empty, nw, sw, Empty) ->
                            let nw', sw' = par2 (fun () -> gen nw (QuadRope.sliceToMatchNW lqr rqr))
                                                (fun () -> gen sw (QuadRope.sliceToMatchSW lqr rqr))
                            thinNode nw' sw'

                        // Full node.
                        | Node (true, _, _, _, ne, nw, sw, se) ->
                            let ne', nw', sw', se' =
                                par4 (fun () -> gen ne (QuadRope.sliceToMatchNE lqr rqr))
                                     (fun () -> gen nw (QuadRope.sliceToMatchNW lqr rqr))
                                     (fun () -> gen sw (QuadRope.sliceToMatchSW lqr rqr))
                                     (fun () -> gen se (QuadRope.sliceToMatchSE lqr rqr))
                            node ne' nw' sw' se'

                        | Sparse (_, _, 0.0) -> lqr
                        | Sparse (_, _, 1.0) -> rqr

                        | _ -> zip (*) lqr rqr

        /// Fast case for quad ropes of equal structure.
        let rec fast lqr rqr =
            match lqr, rqr with
                // Recurse on nodes if at least one of them is sparse.

                // Flat node.
                | Node (true, _, _, _, lne, lnw, Empty, Empty), Node (_, _, _, _, rne, rnw, Empty, Empty)
                | Node (_, _, _, _, lne, lnw, Empty, Empty), Node (true, _, _, _, rne, rnw, Empty, Empty)
                    when QuadRope.subShapesMatch lqr rqr ->
                        let ne, nw = par2 (fun () -> fast lne rne)
                                          (fun () -> fast lnw rnw)
                        flatNode nw ne

                // Thin node.
                | Node (true, _, _, _, Empty, lnw, lsw, Empty), Node (_, _, _, _, Empty, rnw, rsw, Empty)
                | Node (_, _, _, _, Empty, lnw, lsw, Empty), Node (true, _, _, _, Empty, rnw, rsw, Empty)
                    when QuadRope.subShapesMatch lqr rqr ->
                        let nw, sw = par2 (fun () -> fast lnw rnw)
                                          (fun () -> fast lsw rsw)
                        thinNode nw sw

                // Full node.
                | Node (_, _, _, _, lne, lnw, lsw, lse), Node (true, _, _, _, rne, rnw, rsw, rse)
                | Node (true, _, _, _, lne, lnw, lsw, lse), Node (_, _, _, _, rne, rnw, rsw, rse)
                    when QuadRope.subShapesMatch lqr rqr ->
                        let ne, nw, sw, se = par4 (fun () -> fast lne rne)
                                                  (fun () -> fast lnw rnw)
                                                  (fun () -> fast lsw rsw)
                                                  (fun () -> fast lse rse)
                        node ne nw sw se

                // It may pay off to materialize if slices are sparse.
                | Slice _, _ when isSparse lqr -> fast (QuadRope.materialize lqr) rqr
                | _, Slice _ when isSparse rqr -> fast lqr (QuadRope.materialize rqr)

                // Sparse quad ropes of zero result in zero.
                | Sparse (h, w, 0.0), _
                | _, Sparse (h, w, 0.0) -> Sparse (h, w, 0.0)

                // Sparse quad ropes of one result in the other quad rope.
                | Sparse (_, _, 1.0), qr
                | qr, Sparse (_, _, 1.0) -> qr

                // Fall back to general case.
                | _ when isSparse lqr or isSparse rqr -> gen lqr rqr
                | _ -> zip (*) lqr rqr

        fast lqr rqr
