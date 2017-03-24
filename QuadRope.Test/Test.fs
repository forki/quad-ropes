// Copyright (c) 2017 Florian Biermann, fbie@itu.dk

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

namespace QuadRopes.Test

module TestRunner =
    open FsCheck

    let test () =
        // Register generators.
        Gen.register() |> ignore

        // Run tests.
        Check.QuickAll (typeof<Utils.Handle>.DeclaringType)
        Check.QuickAll (typeof<QuadRope.Handle>.DeclaringType)
        Check.QuickAll (typeof<Parallel.QuadRope.Handle>.DeclaringType)
        Check.QuickAll (typeof<Interesting.QuadRope.Handle>.DeclaringType)
        Check.QuickAll (typeof<Examples.Handle>.DeclaringType)
    [<EntryPoint>]
    let main _ =
        try
            test()
            0
        with
            | e -> printfn "%A" e; 1
