/// Contains error propagation functions and a computation expression builder for Railway-oriented programming.
module Chessie.ErrorHandling

open System

/// Represents the result of a computation.
type Result<'TSuccess, 'TMessage> =    
    /// Represents the result of a successful computation.
    | Pass of 'TSuccess * 'TMessage list
    /// Represents the result of a failed computation.
    | Fail of 'TMessage list

/// Wraps a value in a Success
let inline pass x = Pass(x,[])

/// Wraps a message in a Failure
let inline fail msg = Fail([msg])

/// Takes a Result and maps it with fSuccess if it is a Success otherwise it maps it with fFailure.
let inline either fSuccess fFailure attemptResult = 
    match attemptResult with
    | Pass(x, msgs) -> fSuccess(x,msgs)
    | Fail(msgs) -> fFailure(msgs)

/// If the given result is a Success the wrapped value will be returned. 
///Otherwise the function throws an exception with Failure message of the result.
let inline returnOrFail result = 
    let inline raiseExn msgs = 
        msgs 
        |> Seq.map (sprintf "%O")
        |> String.concat (Environment.NewLine + "\t")
        |> failwith
    either fst raiseExn result

/// Appends the given messages with the messages in the given result.
let inline mergeMessages msgs result =
    let inline fSuccess (x,msgs2) = Pass (x, msgs @ msgs2) 
    let inline fFailure errs = Fail (errs @ msgs) 
    either fSuccess fFailure result

/// If the result is a Success it executes the given function on the value.
/// Otherwise the exisiting failure is propagated.
let inline bind f result =
    let inline fSuccess (x, msgs) = f x |> mergeMessages msgs
    let inline fFailure (msgs) = Fail msgs
    either fSuccess fFailure result

/// If the result is a Success it executes the given function on the value. 
/// Otherwise the exisiting failure is propagated.
/// This is the infix operator version of ErrorHandling.bind
let inline (>>=) result f = bind f result

/// If the wrapped function is a success and the given result is a success the function is applied on the value. 
/// Otherwise the exisiting error messages are propagated.
let inline apply wrappedFunction result = 
    match wrappedFunction, result with
    | Pass(f, msgs1), Pass(x, msgs2) -> Pass(f x, msgs1 @ msgs2)
    | Fail errs, Pass(_, msgs) -> Fail(errs @ msgs)
    | Pass(_, msgs), Fail errs -> Fail(errs @ msgs)
    | Fail errs1, Fail errs2 -> Fail(errs1 @ errs2)

/// If the wrapped function is a success and the given result is a success the function is applied on the value. 
/// Otherwise the exisiting error messages are propagated.
/// This is the infix operator version of ErrorHandling.apply
let inline (<*>) wrappedFunction result = apply wrappedFunction result

/// Lifts a function into a Result container and applies it on the given result.
let inline lift f result = apply (pass f) result

/// Lifts a function into a Result and applies it on the given result.
/// This is the infix operator version of ErrorHandling.lift
let inline (<!>) f result = lift f result 

/// If the result is a Success it executes the given function on the value and the messages.
/// Otherwise the exisiting failure is propagated.
let inline successTee f result = 
    let inline fSuccess (x,msgs) = 
        f (x,msgs)
        Pass (x,msgs) 
    let inline fFailure errs = Fail errs 
    either fSuccess fFailure result

/// If the result is a Failure it executes the given function on the value and the messages. 
/// Otherwise the exisiting successful value is propagated.
let inline failureTee f result = 
    let inline fSuccess (x,msgs) = Pass (x,msgs) 
    let inline fFailure errs = 
        f errs
        Fail errs 
    either fSuccess fFailure result

/// Collects a sequence of Results and accumulates their values.
/// If the sequence contains an error the error will be propagated.
let inline collect xs = 
    Seq.fold (fun result next -> 
        match result, next with
        | Pass(rs, m1), Pass(r, m2) -> Pass(r :: rs, m1 @ m2)
        | Pass(_, m1), Fail(m2) | Fail(m1), Pass(_, m2) -> Fail(m1 @ m2)
        | Fail(m1), Fail(m2) -> Fail(m1 @ m2)) (pass []) xs
    |> lift List.rev

/// Converts an option into a Result.
let inline failIfNone message result =
    match result with
    | Some x -> pass x
    | None -> fail message

/// Builder type for error handling computation expressions.
type ErrorHandlingBuilder() =
    member __.Zero() = pass ()
    member __.Bind(m, f) = bind f m
    member __.Return(x) = pass x
    member __.ReturnFrom(x) = x

/// Wraps computations in a error handling computation expression.
let trial = ErrorHandlingBuilder()
