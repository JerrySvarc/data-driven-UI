module Editor.Operations.CustomRendering

open CoreLogic.Operations.RenderingCode
open CoreLogic.Types.RenderingTypes
open CoreLogic.Operations.DataRecognition
open Fable.SimpleJson
open Editor.Types.EditorModel
open Feliz
open Fable.React
open Fable.Core.JsInterop

//Custom rendering function for displaying preview with interwoven menus for the elements
let rec renderingCodeToReactElement
    (code: RenderingCode)
    (path: int list)
    (json: Json)
    (name: string)
    (options: (Msg -> unit) -> RenderingCode -> list<int> -> string -> ReactElement)
    (showOptions: bool)
    (dispatch: Msg -> unit)
    : ReactElement =

    let renderWithOptions (preview: ReactElement) =
        if showOptions then
            Html.div [
                prop.className "border border-secondary-900 bg-white rounded-md m-1"
                prop.children [ preview; options dispatch code path name ]
            ]
        else
            preview

    let createPreview (tag: string) (attributes: obj) (children: ReactElement list) =
        try
            if tag.ToLower() = "input" then
                ReactBindings.React.createElement (tag, attributes, [])
            else
                ReactBindings.React.createElement (tag, attributes, children)
        with ex ->
            Html.div [ prop.className "error-message"; prop.text $"Unexpected error: {ex.Message}" ]

    let renderHtmlElement (tag: Tag) (attrs: Attributes) (innerValue: InnerValue) =
        let attributes =
            attrs
            |> List.map (fun (key, value) -> (key, box value))
            |> List.append [ ("className", box "preview") ]
            |> createObj

        let children =
            match innerValue with
            | Data ->
                try
                    let jsonStr = json |> Json.convertFromJsonAs<string>
                    [ Html.text jsonStr ]
                with ex -> [ Html.text $"Data parsing error: {ex.Message}" ]
            | InnerValue.Empty -> []
            | Constant value -> [ Html.text value ]

        createPreview (tagToString tag) attributes children |> renderWithOptions

    let renderHtmlList (listType: ListType) (codes: RenderingCode list) =
        match json with
        | JArray array ->
            let elements =
                codes
                |> List.mapi (fun index code ->
                    let arrayItem = List.item index array
                    let showOptionsForItem = index = 0 && showOptions

                    let renderedItem =
                        renderingCodeToReactElement
                            code
                            (path @ [ index ])
                            arrayItem
                            name
                            options
                            showOptionsForItem
                            dispatch

                    Html.li [ prop.className "preview"; prop.children [ renderedItem ] ])

            let listTag = listTypeToString listType

            createPreview listTag (createObj [ "className" ==> "preview" ]) elements
            |> renderWithOptions
        | _ -> Html.div [ prop.text "Invalid JSON for HtmlList: not an array" ]

    let renderHtmlObject (keys: string list) (codes: Map<string, RenderingCode>) =
        match json with
        | JObject object ->
            let renderedElements =
                keys
                |> List.mapi (fun index key ->
                    let element = codes.TryFind key
                    let jsonValue = object.TryFind key

                    match element, jsonValue with
                    | Some code, Some value ->
                        renderingCodeToReactElement code (path @ [ index ]) value key options showOptions dispatch
                    //TODO: styling
                    | None, Some(_) ->
                        Html.div [ prop.text ("RenderingCode element with the name " + key + " not found.") ]
                    | Some(_), None ->
                        Html.div [ prop.text ("JSON object value with the name " + key + " not found.") ]
                    | None, None ->
                        Html.div [
                            prop.text (
                                "JSON object value and RenderingCode element  with the name "
                                + key
                                + " not found."
                            )
                        ])

            Html.div [ prop.className "preview"; prop.children renderedElements ]
            |> renderWithOptions
        | _ -> Html.div [ prop.text "Invalid JSON for Sequence: not an object" ]



    let renderHole (named: FieldHole) =
        let holeName =
            match named with
            | UnNamed -> "Unnamed"
            | Named n -> n

        let fieldType = recognizeJson json
        options dispatch fieldType path holeName

    let renderCustomWrapper (customWrapper: CustomWrapper) = failwith "Not implemented yet"

    let renderCustomElement (customElement: CustomElement) = failwith "Not implemented yet"


    match code with
    | HtmlElement(tag, attrs, innerValue, eventHandlers) -> renderHtmlElement tag attrs innerValue
    | HtmlList(listType, codes, eventHandlers) -> renderHtmlList listType codes
    | HtmlObject(objType, keys, codes, eventHandlers) -> renderHtmlObject keys codes
    | Hole named -> renderHole named
    | CustomWrapper(customWrapper) -> failwith "Not implemented yet"
    | CustomElement(customElement) -> failwith "Not implemented yet"