module Editor.UIComponents.PageEditorComponents

open Editor.Types.EditorDomain
open Editor.Types.PageEditorDomain
open Fable.React
open Feliz
open CoreLogic.Types.RenderingTypes
open Elmish
open Fable.Core.JsInterop
open Fable.SimpleJson
open Editor.Utilities.FileUpload
open Editor.Utilities.Icons
open Editor.Utilities.JsonParsing
open Editor.Components.CustomRendering
open CoreLogic.Operations.DataRecognition
open CoreLogic.Operations.RenderingCode
open CoreLogic.Operations.CodeGeneration
open Editor.Utilities.JavaScriptEditor
open System
open Editor.Components.ElementComponents

// PageEditor elmish-style functionality

let pageEditorInit () : PageEditorModel * Cmd<PageEditorMsg> =
    let newPage = {
        Name = "New page"
        Id = Guid.NewGuid()
        ParsedJson = JNull
        CurrentTree = Hole(UnNamed)
        JsonString = ""
        CustomHandlers = Map([])
    }

    let initialItems = [
        {
            Id = 1
            Position = { X = 100.0; Y = 150.0 }
            Content = Html.div []
        }
        {
            Id = 2
            Position = { X = 300.0; Y = 400.0 }
            Content = Html.div []
        }
        {
            Id = 3
            Position = { X = 500.0; Y = 250.0 }
            Content = Html.div []
        }
    ]

    let newPageEditorModel = {
        PageData = newPage
        FileUploadError = false
        ViewportPosition = { X = 0.0; Y = 0.0 }
        Scale = 1.0
        Elements = initialItems
        DraggingElementId = None
        IsPanning = false
        LastMousePosition = None
        IsPreviewOpen = false
    }

    newPageEditorModel, Cmd.none


// This function is used to update the page editor model
// It is called when the user interacts with the page editor
// It updates the page editor model and sends a message to the main page via Cmd.ofMsg

let pageEditorUpdate (msg: PageEditorMsg) (model: PageEditorModel) : PageEditorModel * Cmd<PageEditorMsg> =
    match msg with
    | UploadData jsonString ->
        let loadedDataOption = loadJson jsonString

        match loadedDataOption with
        | Some(data) ->
            match data with
            | JObject obj ->
                let updatedPage = {
                    model.PageData with
                        CurrentTree = recognizeJson data
                        ParsedJson = data
                        JsonString = jsonString
                }

                let newModelElement = {
                    Id = model.Elements.Length + 1
                    Position = { X = 400.0; Y = 350.0 }
                    Content = ModelElement updatedPage.ParsedJson
                }

                let updatedEditorPage = {
                    model with
                        PageData = updatedPage
                        FileUploadError = false
                        Elements = model.Elements @ [ newModelElement ]
                }

                updatedEditorPage, Cmd.ofMsg (SyncWithMain updatedEditorPage)
            | _ -> { model with FileUploadError = true }, Cmd.none
        | None -> { model with FileUploadError = true }, Cmd.none

    | ReplaceCode(code, path) ->
        let newCodes = replace path code model.PageData.CurrentTree

        let updatedPage = {
            model.PageData with
                CurrentTree = newCodes
        }

        let newModel = { model with PageData = updatedPage }
        newModel, Cmd.ofMsg (SyncWithMain newModel)

    | SyncWithMain _ -> model, Cmd.none
    | StartPanning pos when model.DraggingElementId.IsNone ->
        {
            model with
                IsPanning = true
                LastMousePosition = Some pos
        },
        Cmd.none

    | UpdatePanning pos when model.IsPanning ->
        match model.LastMousePosition with
        | Some lastPos ->
            let dx = (pos.X - lastPos.X) / model.Scale
            let dy = (pos.Y - lastPos.Y) / model.Scale

            let newViewport = {
                X = model.ViewportPosition.X + dx
                Y = model.ViewportPosition.Y + dy
            }

            {
                model with
                    ViewportPosition = newViewport
                    LastMousePosition = Some pos
            },
            Cmd.none
        | None -> model, Cmd.none

    | EndPanning ->
        {
            model with
                IsPanning = false
                LastMousePosition = None
        },
        Cmd.none

    | StartDraggingItem(itemId, pos) ->
        {
            model with
                DraggingElementId = Some itemId
                LastMousePosition = Some pos
        },
        Cmd.none

    | UpdateDraggingItem pos ->
        match model.DraggingElementId, model.LastMousePosition with
        | Some itemId, Some lastPos ->
            let dx = (pos.X - lastPos.X) / model.Scale
            let dy = (pos.Y - lastPos.Y) / model.Scale

            let updatedItems =
                model.Elements
                |> List.map (fun item ->
                    if item.Id = itemId then
                        {
                            item with
                                Position = {
                                    X = item.Position.X + dx
                                    Y = item.Position.Y + dy
                                }
                        }

                    else
                        item)

            {
                model with
                    Elements = updatedItems
                    LastMousePosition = Some pos
            },
            Cmd.none
        | _ -> model, Cmd.none

    | EndDraggingItem ->
        {
            model with
                DraggingElementId = None
                LastMousePosition = None
        },
        Cmd.none

    | Zoom scaleFactor ->
        {
            model with
                Scale = model.Scale * scaleFactor
        },
        Cmd.none

    | AddItem pos ->
        let newItem = {
            Id = model.Elements.Length
            Position = { X = pos.X; Y = pos.Y }
            Content = Html.div []
        }

        {
            model with
                Elements = newItem :: model.Elements
        },
        Cmd.none

    | TogglePreview ->
        {
            model with
                IsPreviewOpen = not model.IsPreviewOpen
        },
        Cmd.none
    | StartPanning(_)
    | UpdatePanning(_) -> failwith "Not Implemented"


[<ReactComponent>]
let JavaScriptEditorView code (dispatch) =

    let extensions = [| javascript?javascript (); html?html (); css?css () |]

    //let onChange = fun value -> dispatch (UpdateHtmlCode value)

    Html.div [
        prop.className "flex flex-col h-full border-solid border-2 border-black overflow-auto"
        prop.children [
            Html.h3 [ prop.className "font-bold mb-2 px-2"; prop.text "Code preview" ]
            Html.div [
                prop.className "flex-grow overflow-auto"
                prop.children [
                    ReactBindings.React.createElement (
                        CodeMirror,
                        createObj [
                            "value" ==> code
                            "extensions" ==> extensions
                            //"onChange" ==> onChange
                            "theme" ==> "dark"
                            "readOnly" ==> "true"
                        ],
                        []
                    )
                ]
            ]
        ]
    ]


// These are the main components that make up the page editor
// They are used to render the page editor and handle user interactions
let DataUpload dispatch =
    let uploadButtonView onLoad =
        Html.div [
            prop.className "inline-flex items-center"
            prop.children [
                Html.label [
                    prop.className
                        "bg-blue-500 hover:bg-blue-600 text-white font-bold py-2 px-2 rounded shadow flex items-center justify-center h-10"
                    prop.children [
                        ReactBindings.React.createElement (
                            uploadIcon,
                            createObj [ "size" ==> 16; "className" ==> "mr-2" ],
                            []
                        )
                        Html.span [ prop.className "font-medium"; prop.text "Upload" ]
                        Html.input [
                            prop.type' "file"
                            prop.className "hidden"
                            prop.onChange (handleFileEvent onLoad)
                        ]
                    ]
                ]
            ]
        ]

    let uploadButton = uploadButtonView (UploadData >> dispatch)

    Html.div [ prop.className "m-2"; prop.children [ uploadButton ] ]



let toolBarElements dispatch = [
    DataUpload dispatch
    Html.button [
        prop.className
            "bg-blue-500 hover:bg-blue-600 text-white font-bold h-10 px-4 rounded shadow flex items-center justify-center "
        prop.text "Save"
    ]
    Html.button [
        prop.className
            "bg-green-500 hover:bg-green-600 text-white font-bold h-10 px-4 rounded shadow flex items-center justify-center leading-none"
        prop.text "Publish"
    ]
    Html.button [
        prop.className
            "bg-yellow-500 hover:bg-yellow-600 text-white font-bold h-10 px-4 rounded shadow flex items-center justify-center leading-none"
        prop.text "Preview Page"
        prop.onClick (fun _ -> dispatch TogglePreview)
    ]
]

let ToolBar dispatch =
    Html.div [
        prop.className
            "flex items-center justify-center bg-gray-500 text-white h-fit w-fit fixed top-5 z-10 shadow-md border-b border-gray-700 rounded"
        prop.children [
            Html.nav [
                prop.className "flex space-x-1 items-center h-full"
                prop.children (toolBarElements dispatch)
            ]
        ]
    ]


[<ReactComponent>]
let SandboxPreviewView (model: PageEditorModel) dispatch =
    let fullHtml, js =
        generateCode model.PageData.CurrentTree model.PageData.JsonString model.PageData.CustomHandlers

    if model.IsPreviewOpen then
        Html.div [
            prop.className "bg-yellow-500"
            prop.children [
                Html.iframe [
                    prop.src "about:blank"
                    prop.custom ("sandbox", "allow-scripts allow-forms allow-modals")
                    prop.custom ("srcDoc", fullHtml)

                ]
            ]
        ]
    else
        Html.none

[<ReactComponent>]
let Canvas (model: PageEditorModel) (dispatch: PageEditorMsg -> unit) =
    Html.div [
        prop.className "relative overflow-hidden w-full h-full bg-gray-600 "
        prop.onMouseDown (fun e ->
            if model.DraggingElementId.IsNone then
                dispatch (StartPanning { X = e.clientX; Y = e.clientY }))

        prop.onMouseMove (fun e ->
            match model.IsPanning, model.DraggingElementId with
            | true, _ -> dispatch (UpdatePanning { X = e.clientX; Y = e.clientY })
            | false, Some _ -> dispatch (UpdateDraggingItem { X = e.clientX; Y = e.clientY })
            | _ -> ())

        prop.onMouseUp (fun _ ->
            dispatch EndPanning
            dispatch EndDraggingItem)

        prop.onWheel (fun e ->
            let scaleFactor = if e.deltaY > 0.0 then 0.9 else 1.1
            dispatch (Zoom scaleFactor))

        prop.children [
            Html.div [ prop.className "relative"; prop.children (renderElements model dispatch) ]
        ]

    ]


[<ReactComponent>]
let PageEditorView (pageModel: PageEditorModel) (dispatch: PageEditorMsg -> unit) =
    Html.div [
        prop.className "relative h-full w-full flex  "
        prop.children [
            ToolBar dispatch
            Canvas pageModel dispatch
            SandboxPreviewView pageModel dispatch
        ]
    ]