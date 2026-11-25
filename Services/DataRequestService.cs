using System.Data;
using System.Text;
using System.Text.Json;
using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels.DataRequests;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class DataRequestService(
    ApplicationDbContext context,
    IIdentifierValidationService identifierValidationService,
    INotificationService notificationService,
    UserManager<ApplicationUser> userManager) : IDataRequestService
{
    private static readonly JsonSerializerOptions s_caseInsensitiveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> s_rejectedInapplicableNumericOrDatePatientColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Age",
        "DateOfBirth",
        "HouseholdSize",
        "PresentationDate",
        "InfectionOnset",
        "WeightKg",
        "TemperatureC",
        "BPSystolic",
        "BPDiastolic",
        "Pulse",
        "OxygenSaturation",
        "AntibioticDays"
    };

    private static readonly HashSet<string> s_isoInapplicableNumericPatientColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "HouseholdSize",
        "WeightKg",
        "TemperatureC",
        "BPSystolic",
        "BPDiastolic",
        "Pulse",
        "OxygenSaturation",
        "AntibioticDays"
    };


    // ============================================================
    // LOAD DATA REQUEST VIEW MODEL
    // ============================================================

    public async Task<DataRequestViewModel> GetDataRequestViewModelAsync(
        CancellationToken cancellationToken = default)
    {
        DataRequestViewModel model = new()
        {
            AvailableDatasets = DataExportCatalogue.GetDatasets(),
            IdentifierTypes = DataExportCatalogue.GetIdentifierTypes(),

            // ====================================================
            // FACILITIES
            // ====================================================

            Facilities = await context.Facilities
                .AsNoTracking()
                .OrderBy(x => x.FacilityName)
                .Select(x => new DataRequestOptionViewModel
                {
                    Value = x.FacilityID,
                    Text = x.FacilityName
                })
                .ToListAsync(cancellationToken),

            // ====================================================
            // ORGANISMS
            // ====================================================

            Organisms = await context.Organisms
                .AsNoTracking()
                .OrderBy(x => x.WHONETID)
                .Select(x => new DataRequestOptionViewModel
                {
                    Value = x.WHONETID,
                    Text = x.WHONETID
                })
                .ToListAsync(cancellationToken)
        };


        // ========================================================
        // DYNAMIC AST COLUMNS & AST CARD PANEL MAP
        // ========================================================

        await AddDynamicASTColumnsAsync(
            model.AvailableDatasets,
            cancellationToken);

        model.AstCardAntibiotics = await BuildAstCardMapAsync(
            cancellationToken);

        return model;
    }


    // ============================================================
    // BUILD AST CARD -> ANTIBIOTIC CODES MAP
    // ============================================================

    private async Task<Dictionary<string, List<string>>> BuildAstCardMapAsync(
        CancellationToken cancellationToken)
    {
        var activeAntibiotics = await context.Antibiotics
            .AsNoTracking()
            .Where(a => a.Active && !string.IsNullOrWhiteSpace(a.ASTCard))
            .Select(a => new
            {
                a.AntibioticCode,
                a.ASTCard
            })
            .ToListAsync(cancellationToken);

        Dictionary<string, HashSet<string>> cardMap = new(StringComparer.OrdinalIgnoreCase);

        foreach (var abx in activeAntibiotics)
        {
            if (string.IsNullOrWhiteSpace(abx.AntibioticCode) ||
                string.IsNullOrWhiteSpace(abx.ASTCard))
            {
                continue;
            }

            var code = abx.AntibioticCode.Trim();
            var cards = abx.ASTCard.Split(
                [',', ';', '|'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var card in cards)
            {
                if (!cardMap.TryGetValue(card, out var codes))
                {
                    codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    cardMap[card] = codes;
                }

                codes.Add(code);
            }
        }

        return cardMap
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }


    // ============================================================
    // ADD DYNAMIC AST / ANTIBIOTIC COLUMNS
    // ============================================================

    private async Task AddDynamicASTColumnsAsync(
        List<DataRequestDatasetViewModel> datasets,
        CancellationToken cancellationToken)
    {
        var worksheetDataset = datasets.FirstOrDefault(
            x => x.Value == "Worksheet");

        if (worksheetDataset is null)
        {
            return;
        }

        var antibiotics = await context.Antibiotics
            .AsNoTracking()
            .Select(x => new
            {
                x.AntibioticCode,
                x.AntibioticName
            })
            .ToListAsync(cancellationToken);

        if (antibiotics.Count == 0)
        {
            return;
        }

        var worksheetColumns = await GetWorksheetColumnsAsync(
            cancellationToken);

        if (worksheetColumns.Count == 0)
        {
            return;
        }

        var worksheetColumnSet = worksheetColumns.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        List<DataRequestColumnViewModel> dynamicColumns = [];

        foreach (var antibiotic in antibiotics)
        {
            if (string.IsNullOrWhiteSpace(antibiotic.AntibioticCode))
            {
                continue;
            }

            var antibioticCode = antibiotic.AntibioticCode.Trim();

            var antibioticName = string.IsNullOrWhiteSpace(antibiotic.AntibioticName)
                ? antibioticCode
                : antibiotic.AntibioticName.Trim();

            var micColumn = $"{antibioticCode}_MIC";

            if (worksheetColumnSet.Contains(micColumn))
            {
                dynamicColumns.Add(
                    new DataRequestColumnViewModel
                    {
                        Value = micColumn,
                        Text = $"{antibioticName}_MIC"
                    });
            }

            var interpretationColumn = $"{antibioticCode}_INT";

            if (worksheetColumnSet.Contains(interpretationColumn))
            {
                dynamicColumns.Add(
                    new DataRequestColumnViewModel
                    {
                        Value = interpretationColumn,
                        Text = $"{antibioticName}_INT"
                    });
            }
        }

        var existingColumns = worksheetDataset.Columns
            .Select(x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var column in dynamicColumns)
        {
            if (!existingColumns.Contains(column.Value))
            {
                worksheetDataset.Columns.Add(column);
            }
        }
    }


    // ============================================================
    // GET ACTUAL dbo.WORKSHEET COLUMNS
    // ============================================================

    private async Task<List<string>> GetWorksheetColumnsAsync(
        CancellationToken cancellationToken)
    {
        List<string> columns = [];

        var connection = context.Database.GetDbConnection();

        var shouldCloseConnection = connection.State != ConnectionState.Open;

        try
        {
            if (shouldCloseConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME = 'Worksheet'
                ORDER BY ORDINAL_POSITION;
                """;

            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }
        }
        finally
        {
            if (shouldCloseConnection && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }

        return columns;
    }


    // ============================================================
    // VALIDATE UPLOADED IDENTIFIER FILE
    // ============================================================

    public async Task<List<DataRequestIdentifierValidationViewModel>> ValidateIdentifierFileAsync(
        string identifierType,
        IFormFile identifierFile,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifierType))
        {
            throw new InvalidOperationException(
                "Please select an identifier type.");
        }

        if (identifierFile is null || identifierFile.Length == 0)
        {
            throw new InvalidOperationException(
                "Please select an Excel file containing identifiers.");
        }

        if (!IsExcelFile(identifierFile))
        {
            throw new InvalidOperationException(
                "The uploaded file must be an Excel (.xlsx or .xls) file.");
        }

        var identifiers = await ReadIdentifiersFromExcelAsync(
            identifierFile,
            identifierType,
            cancellationToken);

        if (identifiers.Count == 0)
        {
            throw new InvalidOperationException(
                "No identifiers were found in the uploaded Excel file.");
        }

        return await identifierValidationService.ValidateIdentifiersAsync(
            identifierType,
            identifiers,
            cancellationToken);
    }


    // ============================================================
    // READ IDENTIFIERS FROM EXCEL
    // ============================================================

    private static async Task<List<string>> ReadIdentifiersFromExcelAsync(
        IFormFile identifierFile,
        string identifierType,
        CancellationToken cancellationToken)
    {
        List<string> identifiers = [];

        await using var stream = identifierFile.OpenReadStream();

        using var memoryStream = new MemoryStream();

        await stream.CopyToAsync(memoryStream, cancellationToken);

        memoryStream.Position = 0;

        using var workbook = new XLWorkbook(memoryStream);

        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return identifiers;
        }

        var usedRange = worksheet.RangeUsed();

        if (usedRange is null)
        {
            return identifiers;
        }

        var normalizedHeaderType = identifierType.Trim();
        var isFirstRow = true;

        foreach (var row in usedRange.Rows())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var value = row.Cell(1).GetString().Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (isFirstRow)
            {
                isFirstRow = false;

                if (string.Equals(value, normalizedHeaderType, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "Identifier", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "ID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            identifiers.Add(value);
        }

        return [.. identifiers.Distinct(StringComparer.OrdinalIgnoreCase)];
    }


    // ============================================================
    // EXCEL FILE CHECK
    // ============================================================

    private static bool IsExcelFile(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);

        return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".xls", StringComparison.OrdinalIgnoreCase);
    }


    // ============================================================
    // CREATE DATA REQUEST
    // ============================================================

    public async Task<int> CreateDataRequestAsync(
        DataRequestViewModel model,
        string requestedByUserID,
        CancellationToken cancellationToken = default)
    {
        if (model.HasIdentifierValidationErrors)
        {
            throw new InvalidOperationException(
                "The request contains identifiers with invalid formats. " +
                "Please correct them before submitting the request.");
        }

        var hasUploadedFile = model.IdentifierFile is { Length: > 0 };
        var hasIdentifierType = !string.IsNullOrWhiteSpace(model.IdentifierType);
        var hasValidatedResults = model.IdentifierValidationResults.Count > 0;

        if ((hasUploadedFile || hasIdentifierType) && !hasValidatedResults)
        {
            throw new InvalidOperationException(
                "Please validate your uploaded identifier file before submitting the request.");
        }

        var selectedData = model.AvailableDatasets
            .Select(dataset => new
            {
                Dataset = dataset.Value,
                Columns = dataset.Columns
                    .Where(column => column.IsSelected)
                    .Select(column => new
                    {
                        column.Value,
                        column.Text
                    })
                    .ToList()
            })
            .Where(x => x.Columns.Count > 0)
            .ToList();

        var filters = new
        {
            model.StartDate,
            model.EndDate,
            model.FacilityIDs,
            model.OrganismIDs
        };

        var identifierValues = model.IdentifierValidationResults
            .Where(x => x.IsPatternValid)
            .Select(x => x.Identifier)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        DataRequest dataRequest = new()
        {
            Reason = model.Reason.Trim(),
            Status = "Pending",
            SelectedData = JsonSerializer.Serialize(selectedData),
            Filters = JsonSerializer.Serialize(filters),
            IdentifierType = string.IsNullOrWhiteSpace(model.IdentifierType)
                ? null
                : model.IdentifierType.Trim(),
            IdentifierValues = identifierValues.Count == 0
                ? null
                : JsonSerializer.Serialize(identifierValues),
            RequestedByUserID = requestedByUserID,
            RequestedAt = EastAfricaTime.Now
        };

        context.DataRequests.Add(dataRequest);

        await context.SaveChangesAsync(cancellationToken);

        var administrators = await userManager.GetUsersInRoleAsync(
            AppRoles.SystemAdministrator);

        var dataExportApprovers = await userManager.GetUsersInRoleAsync(
            AppRoles.DataExportApprover);

        var adminIds = administrators
            .Select(a => a.Id)
            .ToHashSet(StringComparer.Ordinal);

        var usersToNotify = administrators
            .Concat(dataExportApprovers)
            .Where(x => x.IsActive)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Where(x =>
                !string.Equals(x.Id, requestedByUserID, StringComparison.Ordinal) ||
                adminIds.Contains(x.Id))
            .ToList();

        foreach (var user in usersToNotify)
        {
            await notificationService.CreateNotificationAsync(
                userId: user.Id,
                title: "New Data Request",
                message:
                    $"A new data request #{dataRequest.DataRequestID} " +
                    "has been submitted and is awaiting review.",
                notificationType: "DataRequest",
                link: $"/ApprovalQueue/Review/{dataRequest.DataRequestID}",
                cancellationToken: cancellationToken);
        }

        return dataRequest.DataRequestID;
    }


    // ============================================================
    // GET CURRENT USER'S DATA REQUESTS
    // ============================================================

    public async Task<List<DataRequest>> GetUserRequestsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await context.DataRequests
            .AsNoTracking()
            .Where(x => x.RequestedByUserID == userId)
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync(cancellationToken);
    }


    // ============================================================
    // GET PENDING DATA REQUESTS
    // ============================================================

    public async Task<List<DataRequest>> GetPendingRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.DataRequests
            .AsNoTracking()
            .Where(x => x.Status == "Pending")
            .OrderBy(x => x.RequestedAt)
            .ToListAsync(cancellationToken);
    }


    // ============================================================
    // GET DATA REQUEST BY ID
    // ============================================================

    public async Task<DataRequest?> GetDataRequestByIdAsync(
        int dataRequestId,
        CancellationToken cancellationToken = default)
    {
        return await context.DataRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DataRequestID == dataRequestId,
                cancellationToken);
    }


    // ============================================================
    // APPROVE DATA REQUEST
    // ============================================================

    public async Task ApproveDataRequestAsync(
        int dataRequestId,
        string reviewedByUserId,
        string? reviewComments,
        CancellationToken cancellationToken = default)
    {
        var request = await context.DataRequests
            .FirstOrDefaultAsync(
                x => x.DataRequestID == dataRequestId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The requested data request could not be found.");

        if (!string.Equals(
                request.Status,
                "Pending",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only pending data requests can be approved.");
        }

        await EnsureReviewerCanReviewAsync(request, reviewedByUserId);

        request.Status = "Approved";
        request.ReviewedByUserID = reviewedByUserId;
        request.ReviewedAt = EastAfricaTime.Now;
        request.ReviewComments = reviewComments;

        await context.SaveChangesAsync(cancellationToken);

        await notificationService.CreateNotificationAsync(
            userId: request.RequestedByUserID,
            title: "Data Request Approved",
            message:
                $"Your data request #{request.DataRequestID} " +
                "has been approved. You can now download the requested data.",
            notificationType: "DataRequest",
            link: "/DataRequest/MyRequests",
            cancellationToken: cancellationToken);
    }


    // ============================================================
    // REJECT DATA REQUEST
    // ============================================================

    public async Task RejectDataRequestAsync(
        int dataRequestId,
        string reviewedByUserId,
        string reviewComments,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reviewComments))
        {
            throw new InvalidOperationException(
                "Please provide comments explaining why the request was rejected.");
        }

        var request = await context.DataRequests
            .FirstOrDefaultAsync(
                x => x.DataRequestID == dataRequestId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The requested data request could not be found.");

        if (!string.Equals(
                request.Status,
                "Pending",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only pending data requests can be rejected.");
        }

        await EnsureReviewerCanReviewAsync(request, reviewedByUserId);

        request.Status = "Rejected";
        request.ReviewedByUserID = reviewedByUserId;
        request.ReviewedAt = EastAfricaTime.Now;
        request.ReviewComments = reviewComments.Trim();

        await context.SaveChangesAsync(cancellationToken);

        await notificationService.CreateNotificationAsync(
            userId: request.RequestedByUserID,
            title: "Data Request Rejected",
            message:
                $"Your data request #{request.DataRequestID} " +
                "was not approved. Please review the comments provided.",
            notificationType: "DataRequest",
            link: "/DataRequest/MyRequests",
            cancellationToken: cancellationToken);
    }


    // ============================================================
    // SEGREGATION OF DUTIES CHECK
    // ============================================================

    private async Task EnsureReviewerCanReviewAsync(
        DataRequest request,
        string reviewedByUserId)
    {
        if (!string.Equals(
                request.RequestedByUserID,
                reviewedByUserId,
                StringComparison.Ordinal))
        {
            return;
        }

        var reviewer = await userManager.FindByIdAsync(reviewedByUserId);
        var isSystemAdmin = reviewer is not null &&
            await userManager.IsInRoleAsync(reviewer, AppRoles.SystemAdministrator);

        if (!isSystemAdmin)
        {
            throw new InvalidOperationException(
                "You cannot review or approve your own data request. Another Data Export Approver or System Administrator must review it.");
        }
    }


    // ============================================================
    // GENERATE EXCEL EXPORT
    // ============================================================

    public async Task<byte[]> GenerateExcelExportAsync(
        DataRequest dataRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataRequest);

        if (!string.Equals(
                dataRequest.Status,
                "Approved",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only approved data requests can be exported.");
        }

        var selectedData = DeserializeSelectedData(dataRequest.SelectedData);

        if (selectedData.Count == 0)
        {
            throw new InvalidOperationException(
                "No data columns were selected for this request.");
        }

        var filters = DeserializeFilters(dataRequest.Filters);

        var identifiers = DeserializeIdentifiers(dataRequest.IdentifierValues);

        var columnDefinitions = await GetExportColumnDefinitionsAsync(
            cancellationToken);

        var selectedColumns = GetSelectedColumns(
            selectedData,
            columnDefinitions);

        if (selectedColumns.Count == 0)
        {
            throw new InvalidOperationException(
                "None of the requested columns are available for export.");
        }

        var exportData = await GetRelationshipAwareExportDataAsync(
            selectedColumns,
            filters,
            dataRequest.IdentifierType,
            identifiers,
            cancellationToken);

        using var workbook = new XLWorkbook();

        var excelWorksheet = workbook.Worksheets.Add("Data Export");

        // --------------------------------------------------------
        // HEADERS (Ensure unique names for ClosedXML Excel Table)
        // --------------------------------------------------------

        HashSet<string> usedHeaders = new(StringComparer.OrdinalIgnoreCase);

        for (var columnIndex = 0; columnIndex < selectedColumns.Count; columnIndex++)
        {
            var col = selectedColumns[columnIndex];
            var headerText = string.IsNullOrWhiteSpace(col.DisplayName)
                ? col.Column
                : col.DisplayName.Trim();

            if (!usedHeaders.Add(headerText))
            {
                headerText = $"{headerText} ({col.Dataset})";
                var suffix = 2;
                var candidate = headerText;
                while (!usedHeaders.Add(candidate))
                {
                    candidate = $"{headerText} {suffix++}";
                }
                headerText = candidate;
            }

            excelWorksheet.Cell(1, columnIndex + 1).Value = headerText;
        }

        var headerRange = excelWorksheet.Range(1, 1, 1, selectedColumns.Count);

        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // --------------------------------------------------------
        // DATA
        // --------------------------------------------------------

        var rowNumber = 2;

        foreach (var row in exportData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var columnIndex = 0; columnIndex < selectedColumns.Count; columnIndex++)
            {
                var selectedColumn = selectedColumns[columnIndex];

                row.TryGetValue(selectedColumn.ExportKey, out var value);

                // ------------------------------------------------
                // SPECIAL ISOLATE ID RULE
                //
                // Where the IsolateID is being used as the
                // PatientID placeholder, export a blank value.
                // ------------------------------------------------

                if (string.Equals(
                        selectedColumn.Dataset,
                        "Worksheet",
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        selectedColumn.Column,
                        "IsolateID",
                        StringComparison.OrdinalIgnoreCase))
                {
                    row.TryGetValue("Worksheet.PatientID", out var patientId);

                    if (value is not null &&
                        patientId is not null &&
                        string.Equals(
                            value.ToString(),
                            patientId.ToString(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        value = null;
                    }
                }

                // ------------------------------------------------
                // PATIENT APPLICABILITY RULE FOR NUMERIC/DATE FIELDS
                //
                // String columns already hold "N/A" in dbo.Patient
                // when inapplicable. Numeric and date columns are
                // NULL in SQL Server and export as "N/A" when the
                // field is inapplicable for that row's workflow.
                // ------------------------------------------------

                if (value is null &&
                    string.Equals(
                        selectedColumn.Dataset,
                        "Patient",
                        StringComparison.OrdinalIgnoreCase) &&
                    IsPatientNumericOrDateNotApplicable(row, selectedColumn.Column))
                {
                    value = "N/A";
                }

                var cell = excelWorksheet.Cell(rowNumber, columnIndex + 1);

                SetExcelCellValue(cell, value);
            }

            rowNumber++;
        }

        // --------------------------------------------------------
        // FORMATTING
        // --------------------------------------------------------

        excelWorksheet.SheetView.FreezeRows(1);

        if (exportData.Count > 0)
        {
            var dataRange = excelWorksheet.Range(
                1,
                1,
                rowNumber - 1,
                selectedColumns.Count);

            dataRange.CreateTable("DataExport");
        }

        excelWorksheet.Columns().AdjustToContents();

        foreach (var column in excelWorksheet.ColumnsUsed())
        {
            if (column.Width > 50)
            {
                column.Width = 50;
            }
        }

        await using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return stream.ToArray();
    }


    private static bool IsPatientNumericOrDateNotApplicable(
        IReadOnlyDictionary<string, object?> row,
        string columnName)
    {
        row.TryGetValue("Patient.AcceptReject", out var acceptRejectObj);
        var acceptReject = acceptRejectObj?.ToString()?.Trim();

        if (string.Equals(acceptReject, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            return s_rejectedInapplicableNumericOrDatePatientColumns.Contains(columnName);
        }

        row.TryGetValue("Patient.PatientID", out var patientIdObj);
        var patientId = patientIdObj?.ToString()?.Trim();
        var isIso = !string.IsNullOrWhiteSpace(patientId) &&
                    patientId.StartsWith("ISO", StringComparison.OrdinalIgnoreCase);

        if (isIso && s_isoInapplicableNumericPatientColumns.Contains(columnName))
        {
            return true;
        }

        if (string.Equals(columnName, "AntibioticDays", StringComparison.OrdinalIgnoreCase))
        {
            row.TryGetValue("Patient.CurrentAntibioticUse", out var abxUseObj);
            var abxUse = abxUseObj?.ToString()?.Trim();
            if (!string.Equals(abxUse, "Yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    // ============================================================
    // MARK REQUEST AS EXPORTED
    // ============================================================

    public async Task MarkRequestAsExportedAsync(
        int dataRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await context.DataRequests
            .FirstOrDefaultAsync(
                x => x.DataRequestID == dataRequestId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The data request could not be found.");

        request.ExportedAt = EastAfricaTime.Now;

        await context.SaveChangesAsync(cancellationToken);
    }


    // ============================================================
    // DESERIALIZE SELECTED DATA
    // ============================================================

    private static List<ExportSelectedDataset> DeserializeSelectedData(
        string? selectedDataJson)
    {
        if (string.IsNullOrWhiteSpace(selectedDataJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ExportSelectedDataset>>(
                       selectedDataJson,
                       s_caseInsensitiveJsonOptions)
                   ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "The selected data stored for this request is invalid.");
        }
    }


    // ============================================================
    // DESERIALIZE FILTERS
    // ============================================================

    private static ExportFilters DeserializeFilters(
        string? filtersJson)
    {
        if (string.IsNullOrWhiteSpace(filtersJson))
        {
            return new ExportFilters();
        }

        try
        {
            return JsonSerializer.Deserialize<ExportFilters>(
                       filtersJson,
                       s_caseInsensitiveJsonOptions)
                   ?? new ExportFilters();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "The filters stored for this request is invalid.");
        }
    }


    // ============================================================
    // DESERIALIZE IDENTIFIERS
    // ============================================================

    private static List<string> DeserializeIdentifiers(
        string? identifierValuesJson)
    {
        if (string.IsNullOrWhiteSpace(identifierValuesJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(
                       identifierValuesJson)
                   ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "The identifiers stored for this request is invalid.");
        }
    }


    // ============================================================
    // BUILD AVAILABLE EXPORT COLUMN DEFINITIONS
    // ============================================================

    private async Task<List<ExportColumnDefinition>> GetExportColumnDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        List<ExportColumnDefinition> definitions = [];

        var datasets = DataExportCatalogue.GetDatasets();

        foreach (var dataset in datasets)
        {
            foreach (var column in dataset.Columns)
            {
                definitions.Add(
                    new ExportColumnDefinition
                    {
                        Dataset = dataset.Value,
                        Column = column.Value,
                        DisplayName = column.Text,
                        ExportKey = $"{dataset.Value}.{column.Value}"
                    });
            }
        }

        var worksheetColumns = await GetWorksheetColumnsAsync(
            cancellationToken);

        var worksheetColumnSet = worksheetColumns.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        var antibiotics = await context.Antibiotics
            .AsNoTracking()
            .Select(x => new
            {
                x.AntibioticCode,
                x.AntibioticName
            })
            .ToListAsync(cancellationToken);

        foreach (var antibiotic in antibiotics)
        {
            if (string.IsNullOrWhiteSpace(antibiotic.AntibioticCode))
            {
                continue;
            }

            var code = antibiotic.AntibioticCode.Trim();

            var name = string.IsNullOrWhiteSpace(antibiotic.AntibioticName)
                ? code
                : antibiotic.AntibioticName.Trim();

            var micColumn = $"{code}_MIC";

            if (worksheetColumnSet.Contains(micColumn))
            {
                definitions.Add(
                    new ExportColumnDefinition
                    {
                        Dataset = "Worksheet",
                        Column = micColumn,
                        DisplayName = $"{name}_MIC",
                        ExportKey = $"Worksheet.{micColumn}"
                    });
            }

            var interpretationColumn = $"{code}_INT";

            if (worksheetColumnSet.Contains(interpretationColumn))
            {
                definitions.Add(
                    new ExportColumnDefinition
                    {
                        Dataset = "Worksheet",
                        Column = interpretationColumn,
                        DisplayName = $"{name}_INT",
                        ExportKey = $"Worksheet.{interpretationColumn}"
                    });
            }
        }

        return definitions;
    }


    // ============================================================
    // GET SELECTED COLUMNS
    // ============================================================

    private static List<ExportColumnDefinition> GetSelectedColumns(
        List<ExportSelectedDataset> selectedData,
        List<ExportColumnDefinition> availableColumns)
    {
        List<ExportColumnDefinition> selectedColumns = [];

        foreach (var dataset in selectedData)
        {
            if (string.IsNullOrWhiteSpace(dataset.Dataset))
            {
                continue;
            }

            foreach (var requestedColumn in dataset.Columns ?? [])
            {
                if (string.IsNullOrWhiteSpace(requestedColumn.Value))
                {
                    continue;
                }

                var definition = availableColumns.FirstOrDefault(
                    x =>
                        string.Equals(
                            x.Dataset,
                            dataset.Dataset,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            x.Column,
                            requestedColumn.Value,
                            StringComparison.OrdinalIgnoreCase));

                if (definition is not null &&
                    !selectedColumns.Any(
                        x => string.Equals(
                            x.ExportKey,
                            definition.ExportKey,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    selectedColumns.Add(definition);
                }
            }
        }

        return selectedColumns;
    }


    // ============================================================
    // RELATIONSHIP-AWARE EXPORT QUERY
    // ============================================================

    private async Task<List<Dictionary<string, object?>>> GetRelationshipAwareExportDataAsync(
        List<ExportColumnDefinition> selectedColumns,
        ExportFilters filters,
        string? identifierType,
        List<string> identifiers,
        CancellationToken cancellationToken)
    {
        List<string> selectExpressions = [];

        List<ExportColumnDefinition> internalColumns = [.. selectedColumns];

        if (selectedColumns.Any(
                x =>
                    string.Equals(
                        x.Dataset,
                        "Worksheet",
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        x.Column,
                        "IsolateID",
                        StringComparison.OrdinalIgnoreCase))
            && !internalColumns.Any(
                x => string.Equals(
                    x.ExportKey,
                    "Worksheet.PatientID",
                    StringComparison.OrdinalIgnoreCase)))
        {
            internalColumns.Add(
                new ExportColumnDefinition
                {
                    Dataset = "Worksheet",
                    Column = "PatientID",
                    DisplayName = "Patient ID",
                    ExportKey = "Worksheet.PatientID"
                });
        }

        // Ensure Patient context columns are available internally when any Patient
        // numeric/date column is selected so applicability ("N/A") can be evaluated.
        var hasPatientNumericOrDateColumn = selectedColumns.Any(
            x => string.Equals(x.Dataset, "Patient", StringComparison.OrdinalIgnoreCase) &&
                 s_rejectedInapplicableNumericOrDatePatientColumns.Contains(x.Column));

        if (hasPatientNumericOrDateColumn)
        {
            EnsureInternalPatientColumn(internalColumns, "PatientID", "Patient ID");
            EnsureInternalPatientColumn(internalColumns, "AcceptReject", "Accept / Reject");
            EnsureInternalPatientColumn(internalColumns, "CurrentAntibioticUse", "Current Antibiotic Use");
        }

        foreach (var column in internalColumns)
        {
            var tableAlias = GetTableAlias(column.Dataset);
            var safeColumn = QuoteIdentifier(column.Column);
            var safeAlias = QuoteIdentifier(column.ExportKey);

            selectExpressions.Add($"{tableAlias}.{safeColumn} AS {safeAlias}");
        }

        if (selectExpressions.Count == 0)
        {
            return [];
        }

        StringBuilder sql = new();

        sql.AppendLine("SELECT DISTINCT");
        sql.AppendLine(string.Join("," + Environment.NewLine, selectExpressions));
        sql.AppendLine("FROM dbo.Patient AS p");
        sql.AppendLine("LEFT JOIN dbo.Facility AS f ON p.[FacilityID] = f.[FacilityID]");
        sql.AppendLine("LEFT JOIN dbo.Worksheet AS w ON p.[PatientID] = w.[PatientID]");
        sql.AppendLine("LEFT JOIN dbo.Organism AS o ON w.[WHONETID] = o.[WHONETID]");
        sql.AppendLine("LEFT JOIN dbo.Shipment AS s ON w.[IsolateID] = s.[IsolateID]");
        sql.AppendLine("LEFT JOIN dbo.Genomics AS g ON w.[IsolateID] = g.[IsolateID] OR s.[KENID] = g.[KENID]");

        List<string> whereConditions = [];
        List<ExportSqlParameter> parameters = [];

        if (filters.StartDate.HasValue)
        {
            whereConditions.Add("p.[EnrollmentDate] >= @StartDate");
            parameters.Add(
                new ExportSqlParameter(
                    "@StartDate",
                    filters.StartDate.Value.Date));
        }

        if (filters.EndDate.HasValue)
        {
            whereConditions.Add("p.[EnrollmentDate] < @EndDate");
            parameters.Add(
                new ExportSqlParameter(
                    "@EndDate",
                    filters.EndDate.Value.Date.AddDays(1)));
        }

        AddInFilter(
            whereConditions,
            parameters,
            "p.[FacilityID]",
            "@Facility",
            filters.FacilityIDs);

        AddInFilter(
            whereConditions,
            parameters,
            "w.[WHONETID]",
            "@Organism",
            filters.OrganismIDs);

        if (!string.IsNullOrWhiteSpace(identifierType) && identifiers.Count > 0)
        {
            var identifierColumn = identifierType.Trim().ToUpperInvariant() switch
            {
                "PATIENTID" => "p.[PatientID]",
                "ISOLATEID" => "w.[IsolateID]",
                "KENID" => "s.[KENID]",
                "MRSNID" => "s.[MRSNID]",
                _ => null
            };

            if (identifierColumn is not null)
            {
                AddInFilter(
                    whereConditions,
                    parameters,
                    identifierColumn,
                    "@Identifier",
                    identifiers);
            }
        }

        if (whereConditions.Count > 0)
        {
            sql.AppendLine("WHERE");
            sql.AppendLine(string.Join(Environment.NewLine + "AND ", whereConditions));
        }

        return await ExecuteExportQueryAsync(
            sql.ToString(),
            parameters,
            internalColumns,
            cancellationToken);
    }


    private static void EnsureInternalPatientColumn(
        List<ExportColumnDefinition> internalColumns,
        string columnName,
        string displayName)
    {
        var exportKey = $"Patient.{columnName}";
        if (!internalColumns.Any(x => string.Equals(x.ExportKey, exportKey, StringComparison.OrdinalIgnoreCase)))
        {
            internalColumns.Add(
                new ExportColumnDefinition
                {
                    Dataset = "Patient",
                    Column = columnName,
                    DisplayName = displayName,
                    ExportKey = exportKey
                });
        }
    }


    // ============================================================
    // EXECUTE EXPORT QUERY
    // ============================================================

    private async Task<List<Dictionary<string, object?>>> ExecuteExportQueryAsync(
        string sql,
        List<ExportSqlParameter> parameters,
        List<ExportColumnDefinition> columns,
        CancellationToken cancellationToken)
    {
        List<Dictionary<string, object?>> results = [];

        var connection = context.Database.GetDbConnection();

        var shouldCloseConnection = connection.State != ConnectionState.Open;

        try
        {
            if (shouldCloseConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var parameterDefinition in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = parameterDefinition.Name;
                parameter.Value = parameterDefinition.Value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }

            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                Dictionary<string, object?> row = new(StringComparer.OrdinalIgnoreCase);

                foreach (var column in columns)
                {
                    var ordinal = reader.GetOrdinal(column.ExportKey);

                    row[column.ExportKey] = reader.IsDBNull(ordinal)
                        ? null
                        : reader.GetValue(ordinal);
                }

                results.Add(row);
            }
        }
        finally
        {
            if (shouldCloseConnection && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }

        return results;
    }


    // ============================================================
    // ADD PARAMETERIZED IN FILTER
    // ============================================================

    private static void AddInFilter(
        List<string> whereConditions,
        List<ExportSqlParameter> parameters,
        string columnName,
        string parameterPrefix,
        List<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return;
        }

        var validValues = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validValues.Count == 0)
        {
            return;
        }

        List<string> parameterNames = [];

        for (var index = 0; index < validValues.Count; index++)
        {
            var parameterName = $"{parameterPrefix}{index}";
            parameterNames.Add(parameterName);

            parameters.Add(
                new ExportSqlParameter(
                    parameterName,
                    validValues[index]));
        }

        whereConditions.Add(
            $"{columnName} IN ({string.Join(", ", parameterNames)})");
    }


    // ============================================================
    // DATASET TO SQL TABLE ALIAS
    // ============================================================

    private static string GetTableAlias(string dataset)
    {
        return dataset switch
        {
            "Patient" => "p",
            "Facility" => "f",
            "Worksheet" => "w",
            "Organism" => "o",
            "Shipment" => "s",
            "Genomics" => "g",
            _ => throw new InvalidOperationException(
                $"Unsupported export dataset: {dataset}.")
        };
    }


    // ============================================================
    // QUOTE SQL IDENTIFIER
    // ============================================================

    private static string QuoteIdentifier(string identifier)
    {
        return $"[{identifier.Replace("]", "]]")}]";
    }


    // ============================================================
    // SET EXCEL CELL VALUE
    // ============================================================

    private static void SetExcelCellValue(
        IXLCell cell,
        object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return;
        }

        switch (value)
        {
            case DateTime dateTime:
                cell.Value = dateTime;
                cell.Style.DateFormat.Format = "yyyy-mm-dd";
                break;

            case DateTimeOffset dateTimeOffset:
                cell.Value = dateTimeOffset.DateTime;
                cell.Style.DateFormat.Format = "yyyy-mm-dd";
                break;

            case bool boolean:
                cell.Value = boolean;
                break;

            case byte byteValue:
                cell.Value = byteValue;
                break;

            case short shortValue:
                cell.Value = shortValue;
                break;

            case int intValue:
                cell.Value = intValue;
                break;

            case long longValue:
                cell.Value = longValue;
                break;

            case float floatValue:
                cell.Value = floatValue;
                break;

            case double doubleValue:
                cell.Value = doubleValue;
                break;

            case decimal decimalValue:
                cell.Value = decimalValue;
                break;

            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }


    // ============================================================
    // INTERNAL EXPORT MODELS
    // ============================================================

    private sealed class ExportSelectedDataset
    {
        public string Dataset { get; set; } = string.Empty;

        public List<ExportSelectedColumn> Columns { get; set; } = [];
    }


    private sealed class ExportSelectedColumn
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }


    private sealed class ExportFilters
    {
        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public List<string> FacilityIDs { get; set; } = [];

        public List<string> OrganismIDs { get; set; } = [];
    }


    private sealed class ExportColumnDefinition
    {
        public string Dataset { get; set; } = string.Empty;
        public string Column { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ExportKey { get; set; } = string.Empty;
    }


    private sealed class ExportSqlParameter(string name, object? value)
    {
        public string Name { get; } = name;
        public object? Value { get; } = value;
    }
}