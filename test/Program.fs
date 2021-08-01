module BinaryToTextEncoding.Tests

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

[<EntryPoint>]
let main argv =

    #if FABLE_COMPILER
    Mocha.runTests Tests.allTests
    #else
    runTestsWithArgs defaultConfig argv Tests.allTests
    #endif


