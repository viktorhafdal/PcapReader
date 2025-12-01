module PcapReader

open System
open System.IO

type PcapFromCsv =
    { Number: int
      Time: DateTime
      Source: string
      Destination: string
      Protocol: string
      Length: string
      Info: string }

let filePath = @"../CSVpcapTest.csv"
let outputPath = @"../CSVpcapTest_Anonymized.csv"

let originalLines = File.ReadAllLines filePath

let getAddressColumns (line: string) =
    let parts = line.Split(',')

    if parts.Length > 3 then
        [| parts.[2]; parts.[3] |]
    else
        [||]

let distinctAddresses =
    originalLines
    |> Array.skip 1 // cause first line is header
    |> Array.collect getAddressColumns
    |> Array.distinct

printfn $"Found {distinctAddresses.Length} distinct addresses."

let random = Random 42

let isMac (address: string) =
    let sep =
        if address.Contains(":") then Some ":"
        elif address.Contains("-") then Some "-"
        else None

    match sep with
    | None -> false
    | Some sep ->
        let parts = address.Split(sep)
        parts.Length = 6 &&
        parts |> Array.forall (fun p ->
            p<> "" &&
            p.Length <= 2 &&
            p |> Seq.forall (fun c -> Char.IsDigit(c) || "abcdefABCDEF".Contains(c)))

let isIpv6 (address: string) =
    address.Contains(":") && not (isMac address)

let generateReplacement (original: string) =
    if original.Contains(".") then // ipv4
        let parts = original.Split('.')
        let anon =
            parts
            |> Array.map (fun p ->
                let width = p.Length
                random.Next(0, 255).ToString().PadLeft(width, '0')
            )
        String.Join(".", anon)

    elif isIpv6 original then // ipv6
        if original.Contains("::") then // compressed ipv6
            let parts = original.Split("::")
            let left =
                if parts.[0] = "" then [||]
                else parts.[0].Split(':')
            let right =
                if parts.Length = 1 || parts.[1] = "" then [||]
                else parts.[1].Split(':')

            let leftAnon =
                left
                |> Array.map (fun _ -> random.Next(0, 65535).ToString("x"))

            let rightAnon =
                right
                |> Array.map (fun _ -> random.Next(0, 65535).ToString("x"))

            match leftAnon, rightAnon with
            | [||], [||] -> "::"
            | [||], r -> "::" + String.Join(":", r)
            | l, [||] -> String.Join(":", l) + "::"
            | l, r -> String.Join(":", l) + "::" + String.Join(":", r)

        else // full ipv6
            let parts = original.Split(':')
            let anon =
                parts
                |> Array.map (fun _ -> random.Next(0, 65535).ToString("x"))
            String.Join(":", anon)
    elif isMac original then
        let sep =
            if original.Contains(":") then "-"
            else "-"
        let parts = original.Split(sep)
        let anon =
            parts
            |> Array.map(fun p ->
                let hex = random.Next(0, 255).ToString("X2")
                
                if (p.ToUpper() = p) then hex
                elif p.ToLower() = p then hex.ToLower()
                else
                    let chars = hex.ToCharArray()
                    for i in 0 .. min (p.Length - 1) (chars.Length - 1) do
                        if Char.IsLower(p.[i]) then
                            chars.[i] <- Char.ToLower(chars.[i])
                    String(chars)
            )

        String.Join(sep, anon)
    else
        original

let anonymizationMap =
    distinctAddresses
    |> Array.map (fun original -> original, generateReplacement original)
    |> dict

let processLine (line: string) =
    let parts = line.Split(',')

    if parts.Length < 7 then
        line
    else
        let originalSource = parts.[2]
        let originalDest = parts.[3]

        let newSource =
            if anonymizationMap.ContainsKey(originalSource) then
                anonymizationMap.[originalSource]
            else
                originalSource

        let newDest =
            if anonymizationMap.ContainsKey(originalDest) then
                anonymizationMap.[originalDest]
            else
                originalDest


        parts.[2] <- newSource
        parts.[3] <- newDest

        String.Join(",", parts)

let anonymizedLines =
    originalLines
    |> Array.mapi (fun i line -> if i = 0 then line else processLine line)

File.WriteAllLines(outputPath, anonymizedLines)
printfn $"Anonymized CSV written to: {outputPath}"
