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

let filePath = @"../TestExamplePcapCsv.csv"
let outputPath = @"../TestExamplePcapCsv_Anonymized.csv"

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

let generateReplacement (original: string) =
    if original.Contains(".") then //ipv4
        $"{random.Next(1, 255)}.{random.Next(1, 255)}.{random.Next(1, 255)}.{random.Next(1, 255)}"
    elif original.Contains("::") || original.Split(':').Length > 6 then //ipv6
        let parts = Array.init 8 (fun _ -> random.Next(0, 65535).ToString("x4"))
        String.Join(":", parts)
    elif original.Split(':').Length = 6 then //MAC addresses
        let parts = Array.init 6 (fun _ -> random.Next(0, 255).ToString("X2"))
        String.Join(":", parts)
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
