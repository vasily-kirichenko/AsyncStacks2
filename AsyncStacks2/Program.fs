open System

type AsyncResultBuilder () =
    member __.Return value : Async<Result<'T, 'Error>> =
        Ok value
        |> async.Return

    member __.ReturnFrom (asyncResult : Async<Result<'T, 'Error>>) =
        asyncResult

    member inline this.Zero () : Async<Result<unit, 'Error>> =
        this.Return ()

    member inline this.Delay (generator : unit -> Async<Result<'T, 'Error>>) : Async<Result<'T, 'Error>> =
        async.Delay generator

    member __.Combine (r1, r2) : Async<Result<'T, 'Error>> =
        async {
        let! r1' = r1
        match r1' with
        | Error error ->
            return Error error
        | Ok () ->
            return! r2
        }

    member __.Bind (value : Async<Result<'T, 'Error>>, binder : 'T -> Async<Result<'U, 'Error>>)
        : Async<Result<'U, 'Error>> =
        async {
        let! value' = value
        match value' with
        | Error error ->
            return Error error
        | Ok x ->
            return! binder x
        }

    member inline __.TryWith (computation : Async<Result<'T, 'Error>>, catchHandler : exn -> Async<Result<'T, 'Error>>)
        : Async<Result<'T, 'Error>> =
        async.TryWith(computation, catchHandler)

    member inline __.TryFinally (computation : Async<Result<'T, 'Error>>, compensation : unit -> unit)
        : Async<Result<'T, 'Error>> =
        async.TryFinally (computation, compensation)

    member inline __.Using (resource : ('T :> System.IDisposable), binder : _ -> Async<Result<'U, 'Error>>)
        : Async<Result<'U, 'Error>> =
        async.Using (resource, binder)

    member this.While (guard, body : Async<Result<unit, 'Error>>) : Async<Result<_,_>> =
        if guard () then
            this.Bind (body, (fun () -> this.While (guard, body)))
        else
            this.Zero ()

    member this.For (sequence : seq<_>, body : 'T -> Async<Result<unit, 'Error>>) =
        this.Using (sequence.GetEnumerator (), fun enum ->
            this.While (
                enum.MoveNext,
                this.Delay (fun () ->
                    body enum.Current)))


let asyncResult = AsyncResultBuilder()
type AsyncResult<'a> = Async<Result<'a, exn>>

let executeReader() = 
    async { 
        printfn "%s" (System.Diagnostics.StackTrace(true).ToString())
        return Ok [1..10] 
    }

let exec (count: int) : AsyncResult<int list> =
    asyncResult {
        return! executeReader()
    }
            
let getTasks() =
    asyncResult {
        let! records = exec 1
        return records.Length
    }
                        
let loadTasksAsync() = 
    asyncResult {
        return! getTasks()
    }

let createMailboxProcessor() = MailboxProcessor.Start <| fun inbox ->
    let rec loop (period: TimeSpan) = 
        async { 
            let! msg = inbox.TryReceive(int period.TotalMilliseconds)
            let! loadResult = loadTasksAsync()
            return! loop (TimeSpan.FromSeconds 10.)
        }
    loop TimeSpan.Zero

[<EntryPoint>]
let main _ = 
    let _ = createMailboxProcessor()
    Console.ReadLine() |> ignore
    0
