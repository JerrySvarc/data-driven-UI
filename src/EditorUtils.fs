module EditorUtils
open Fable.React
open Types
open Fable.SimpleJson
open System
open DataRecognition

let rec replace path replacementElement (currentCode: RenderingCode) =
    match path with
    | [] -> replacementElement
    | head :: tail ->
        match currentCode with
        | HtmlList(lt, n, item) ->
            let newItems =
                match item with
                | Hole _ -> item
                | _ -> replace tail replacementElement item

            HtmlList(lt, n, newItems)
        | Sequence(items) ->
            let newItems =
                items
                |> List.mapi (fun i item ->
                    if i = head then
                        replace tail replacementElement item
                    else
                        item)

            Sequence(newItems)
        | _ -> currentCode


let rec renderingCodeToReactElement (code: RenderingCode) (path: int list) (json: Json) (options ): ReactElement =
        match code with
        | HtmlElement(tag, attrs, innerText) ->
            let props = attrs |> List.toSeq |> dict

            match innerText with
            | Data ->
                let selectedFields = json
                let jsonStr = selectedFields |> Json.convertFromJsonAs<String>
                ReactBindings.React.createElement (tag,  props , [ str jsonStr ])
            | Value.Empty -> ReactBindings.React.createElement (tag, props, [])
            | Constant s -> ReactBindings.React.createElement (tag,  props , [ str s ])
        | HtmlList(listType, numbered, code) ->
            ReactBindings.React.createElement ("div", [], [ options code path "List" ])
        | Sequence codes ->
            let jsonList =
                match json with
                | JObject object -> object |> Map.toList
                | _ -> failwith "Not a sequence"

            let renderedElements =
                List.mapi
                    (fun index code ->
                        let (_, jsonSubObject) = List.item index jsonList
                        renderingCodeToReactElement code (path @ [ index ]) jsonSubObject options)
                    codes

            ReactBindings.React.createElement ("div", [], renderedElements)
        | Hole named ->
            let name =
                match named with
                | UnNamed -> "Unnamed"
                | Named name -> name

            let fieldType = json |> recognizeJson
            let optionPane = options fieldType path name
            optionPane