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

let getIpColumns (line: string) =
    let parts = line.Split(',')

    if parts.Length > 3 then
        [| parts.[2]; parts.[3] |] // source & destination
    else
        [||]

let distinctIps =
    originalLines
    |> Array.skip 1 // cause first line is header
    |> Array.collect getIpColumns
    |> Array.distinct

printfn $"Found {distinctIps.Length} distinct IP addresses."

let random = Random(42)

let generateRandomIp () =
    $"{random.Next(1, 255)}.{random.Next(1, 255)}.{random.Next(1, 255)}.{random.Next(1, 255)}."

let newIps = Array.init distinctIps.Length (fun _ -> generateRandomIp ())

let anonymizationMap = (distinctIps, newIps) ||> Array.zip |> dict

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
