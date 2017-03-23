﻿namespace RadTrees.QuadRope.Object

open System

open RadTrees
open RadTrees.Types
open RadTrees.Utils

/// An object wrapper for the functional quad rope type.
type QuadRope<'a when 'a : equality> private (qr : 'a Types.QuadRope) =

    // The actual quad rope structure.
    member private this.qr = qr


    /// Initialize a new quad rope of r rows and c cols. The value at
    /// each index pair (i, h) is initialized by calling function f on
    /// the index values.
    static member Init(r, c, f : _ -> _ -> 'a) =
        new QuadRope<'a>(QuadRope.init r c f)

    /// Initialize a new quad rope of r rows and c cols in
    /// parallel. The value at each index pair (i, h) is initialized
    /// by calling function f on the index values.
    static member InitPar(r, c, f : _ -> _ -> 'a) =
        new QuadRope<'a>(Parallel.QuadRope.init r c f)

    /// Initialize a new quad rope of r rows and c cols. The value at
    /// each index pair (i, h) is initialized by calling Func f on
    /// the index values.
    static member InitFunc(r, c, f : _ -> _ -> 'a) =
        new QuadRope<'a>(QuadRope.init r c f)

    /// Initialize a new quad rope of r rows and c cols in
    /// parallel. The value at each index pair (i, h) is initialized
    /// by calling Func f on the index values.
    static member InitFuncPar(r, c, f : _ -> _ -> 'a) =
        new QuadRope<'a>(Parallel.QuadRope.init r c f)



    /// The number of rows in this quad rope.
    member this.Rows = QuadRope.rows this.qr

    /// The number of columns in this quad rope.
    member this.Cols = QuadRope.cols this.qr

    /// True if this quad rope is empty; false otherwise.
    member this.IsEmpty = QuadRope.isEmpty this.qr



    /// Get the value at row i, column j.
    member this.Get(i, j) =
        QuadRope.get this.qr i j

    /// Set the value at row i, column j to v.
    member this.Set(i, j, v) =
        new QuadRope<'a>(QuadRope.set this.qr i j v)



    /// Combine this and another quad rope in row-direction. The
    /// elements of this quad rope will be "above" the elements of the
    /// other quad rope.
    member this.VCat(other : QuadRope<_>) =
        new QuadRope<'a>(QuadRope.vcat this.qr other.qr)

    /// Combine this and another quad rope in column-direction. The
    /// elements of this quad rope will be "left of" the elements of
    /// the other quad rope.
    member this.HCat(other : QuadRope<_>) =
        new QuadRope<'a>(QuadRope.hcat this.qr other.qr)



    /// Reverse the row order.
    member this.ReverseRows() =
        new QuadRope<'a>(QuadRope.vrev this.qr)

    /// Reverse the row order in parallel.
    member this.ReverseRowsPar() =
        new QuadRope<'a>(Parallel.QuadRope.vrev this.qr)

    /// Reverse the column order.
    member this.ReverseCols() =
        new QuadRope<'a>(QuadRope.hrev this.qr)

    /// Reverse the column order in parallel.
    member this.ReverseColsPar() =
        new QuadRope<'a>(Parallel.QuadRope.hrev this.qr)

    /// Transpose the quad rope.
    member this.Transpose() =
        new QuadRope<'a>(QuadRope.transpose this.qr)

    /// Transpose the quad rope.
    member this.TransposePar() =
        new QuadRope<'a>(Parallel.QuadRope.transpose this.qr)



    /// Apply function f to all elements.
    member this.Map f =
        new QuadRope<'b>(QuadRope.map f this.qr)

    /// Apply function to to all elements and their indices.
    member this.Mapi f =
        new QuadRope<'b>(QuadRope.mapi f this.qr)

    /// Apply a Func to all elements.
    member this.MapFunc f =
        new QuadRope<'b>(QuadRope.map (Functions.toFunc1 f) this.qr)

    /// Apply a Func to all elements and their indices.
    member this.MapiFunc f =
        new QuadRope<'b>(QuadRope.mapi (Functions.toFunc3 f) this.qr)

    /// Apply function f to all elements.
    member this.MapPar f =
        new QuadRope<'b>(Parallel.QuadRope.map f this.qr)

    /// Apply function to to all elements and their indices.
    member this.MapiPar f =
        new QuadRope<'b>(Parallel.QuadRope.mapi f this.qr)

    /// Apply a Func to all elements.
    member this.MapFuncPar f =
        new QuadRope<'b>(Parallel.QuadRope.map (Functions.toFunc1 f) this.qr)

    /// Apply a Func to all elements and their indices.
    member this.MapiFuncPar f =
        new QuadRope<'b>(Parallel.QuadRope.mapi (Functions.toFunc3 f) this.qr)



    /// Map function f to all elements and then combine the results
    /// with function g, starting from the initial value epsilon.
    member this.MapReduce(f, g, epsilon) =
        QuadRope.mapreduce f g epsilon this.qr

    /// Map Func f to all elements and then combine the results
    /// with function g, starting from the initial value epsilon.
    member this.MapReduceFunc(f, g, epsilon) =
        QuadRope.mapreduce (Functions.toFunc1 f) (Functions.toFunc2 g) epsilon this.qr

    /// Combine all elements using function g, starting from epsilon.
    member this.Reduce(g, epsilon) =
        QuadRope.reduce g epsilon this.qr

    /// Combine all elements using Func g, starting from epsilon.
    member this.ReduceFunc(g, epsilon) =
        QuadRope.reduce (Functions.toFunc2 g) epsilon this.qr



    /// Map function f to all elements and then combine the results
    /// with function g, starting from the initial value epsilon.
    member this.MapReducePar(f, g, epsilon) =
        Parallel.QuadRope.mapreduce f g epsilon this.qr

    /// Map Func f to all elements and then combine the results
    /// with function g, starting from the initial value epsilon.
    member this.MapReduceFuncPar(f, g, epsilon) =
        Parallel.QuadRope.mapreduce (Functions.toFunc1 f) (Functions.toFunc2 g) epsilon this.qr

    /// Combine all elements using function g, starting from epsilon.
    member this.ReducePar(g, epsilon) =
        Parallel.QuadRope.reduce g epsilon this.qr

    /// Combine all elements using Func g, starting from epsilon.
    member this.ReduceFuncPar(g , epsilon) =
        Parallel.QuadRope.reduce (Functions.toFunc2 g) epsilon this.qr



    /// Combine this and another quad rope element-wise using function
    /// f. Requires that both quad ropes have equally many rows and
    /// columns. Will throw if this is not the case.
    member this.ZipWith(f, other : QuadRope<'b>) =
        new QuadRope<'c>(QuadRope.zip f this.qr other.qr)

    /// Combine this and another quad rope element-wise using Func
    /// f. Requires that both quad ropes have equally many rows and
    /// columns. Will throw if this is not the case.
    member this.ZipWithFunc(f, other : QuadRope<'b>) =
        new QuadRope<'c>(QuadRope.zip (Functions.toFunc2 f) this.qr other.qr)

    /// Combine this and another quad rope element-wise in parallel
    /// using function f. Requires that both quad ropes have equally
    /// many rows and columns. Will throw if this is not the case.
    member this.ZipWithPar(f, other : QuadRope<'b>) =
        new QuadRope<'c>(Parallel.QuadRope.zip f this.qr other.qr)

    /// Combine this and another quad rope element-wise in parallel
    /// using Func f. Requires that both quad ropes have equally many
    /// rows and columns. Will throw if this is not the case.
    member this.ZipWithFuncPar(f, other : QuadRope<'b>) =
        new QuadRope<'c>(Parallel.QuadRope.zip (Functions.toFunc2 f) this.qr other.qr)



    /// Compute the partial prefix sum of the elements in this quad
    /// rope using function f and starting all columns and rows from
    /// epsilon.
    member this.Scan(f, epsilon) =
        new QuadRope<'a>(QuadRope.scan f epsilon this.qr)

    /// Compute the partial prefix sum of the elements in this quad
    /// rope using Func f and starting all columns and rows from
    /// epsilon.
    member this.ScanFunc(f, epsilon) =
        new QuadRope<'a>(QuadRope.scan (Functions.toFunc4 f) epsilon this.qr)

    /// Compute the partial prefix sum of the elements in this quad
    /// rope in parallel using function f and starting all columns and
    /// rows from epsilon.
    member this.ScanPar(f, epsilon) =
        new QuadRope<'a>(Parallel.QuadRope.scan f epsilon this.qr)

    /// Compute the partial prefix sum of the elements in this quad
    /// rope in parallel using Func f and starting all columns and
    /// rows from epsilon.
    member this.ScanFuncPar(f, epsilon) =
        new QuadRope<'a>(Parallel.QuadRope.scan (Functions.toFunc4 f) epsilon this.qr)


    /// Compute the partial prefix sum of each row using function f
    /// and starting from epsilon.
    member this.RowScan(f, epsilon) =
        new QuadRope<'a>(QuadRope.hscan f (fun _ -> epsilon) this.qr)

    /// Compute the partial prefix sum of each row using Func f
    /// and starting from epsilon.
    member this.RowScanFunc(f, epsilon) =
        new QuadRope<'a>(QuadRope.hscan (Functions.toFunc2 f) (fun _ -> epsilon) this.qr)

    /// Compute the partial prefix sum of each column using function f
    /// and starting from epsilon.
    member this.ColScan(f, epsilon) =
        new QuadRope<'a>(QuadRope.vscan f (fun _ -> epsilon) this.qr)

    /// Compute the partial prefix sum of each column using function f
    /// and starting from epsilon.
    member this.ColScanFunc(f, epsilon) =
        new QuadRope<'a>(QuadRope.vscan (Functions.toFunc2 f) (fun _ -> epsilon) this.qr)
