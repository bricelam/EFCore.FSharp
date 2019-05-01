﻿namespace Bricelam.EntityFrameworkCore.FSharp.Scaffolding.Internal

open System.IO
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Scaffolding.Internal

open Bricelam.EntityFrameworkCore.FSharp.Scaffolding
open Bricelam.EntityFrameworkCore.FSharp.Scaffolding.ScaffoldingTypes
open Bricelam.EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Microsoft.EntityFrameworkCore.Internal


type FSharpModelGenerator
    (dependencies : ModelCodeGeneratorDependencies,
     contextGenerator : ICSharpDbContextGenerator,
     entityTypeGenerator : ICSharpEntityTypeGenerator) =
    inherit ModelCodeGenerator(dependencies)
 
    let fileExtension = ".fs"

    let defaultNamespaces = [
        "System";
        "System.Collections.Generic";
    ]

    let annotationNamespaces = [
        "System.ComponentModel.DataAnnotations";
        "System.ComponentModel.DataAnnotations.Schema";
    ]

    let getNamespacesFromModel (model:IModel) =
        model.GetEntityTypes()
        |> Seq.collect (fun e -> e.GetProperties())
        |> Seq.collect (fun p -> getNamespaces p.ClrType)
        |> Seq.filter (fun ns -> defaultNamespaces |> Seq.contains ns |> not)
        |> Seq.distinct
        |> Seq.sort

    let createDomainFileContent (model:IModel) (useDataAnnotations:bool) (``namespace``:string) domainFileName =

        let namespaces =
            if useDataAnnotations then
                defaultNamespaces |> Seq.append annotationNamespaces |> Seq.append (model |> getNamespacesFromModel)
            else
                defaultNamespaces  |> Seq.append (model |> getNamespacesFromModel)

        let writeNamespaces ``namespace`` (sb:IndentedStringBuilder) =
            sb
                |> append "namespace " |> appendLine ``namespace``
                |> appendEmptyLine
                |> writeNamespaces namespaces
                |> appendEmptyLine

        IndentedStringBuilder()
                |> writeNamespaces ``namespace``
                |> append "module rec " |> append domainFileName |> appendLine " ="
                |> appendEmptyLine

    override __.Language = "F#"

    override __.GenerateModel(model: IModel, ``namespace``: string, contextDir: string, contextName: string, connectionString: string, options: ModelCodeGenerationOptions) =
        let resultingFiles = ScaffoldedModel()

        let generatedCode = contextGenerator.WriteCode(model, ``namespace``, contextName, connectionString, options.UseDataAnnotations, options.SuppressConnectionStringWarning)

        let dbContextFileName = contextName + fileExtension;

        let contextFile =
            ScaffoldedFile(
                Code = generatedCode,
                Path = Path.Combine(contextDir, dbContextFileName))
                
        resultingFiles.ContextFile <- contextFile

        let domainFileName = contextName.Replace("Context", "Domain")

        let domainFile = ScaffoldedFile()
        domainFile.Path <- (domainFileName + fileExtension)

        let domainFileBuilder = createDomainFileContent model options.UseDataAnnotations ``namespace`` domainFileName

        model.GetEntityTypes()
            |> Seq.iter(fun entityType -> 
                domainFileBuilder
                    |> append (entityTypeGenerator.WriteCode(entityType, ``namespace``, options.UseDataAnnotations))
                    |> ignore
            )
        domainFile.Code <- (domainFileBuilder |> string)

        resultingFiles.AdditionalFiles.Add(domainFile)

        resultingFiles

