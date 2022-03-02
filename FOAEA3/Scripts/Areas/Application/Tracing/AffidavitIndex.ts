﻿
$(document).ready(function (): void {

    let columnData: DataTables.ColumnSettings[] = [
        { title: "Action        " },
        { title: "Service       " },
        { title: "Submitter     " },
        { title: "Ctrl Code     " },
        { title: "Date Received " },
        { title: "Debtor SurName" },
        { title: "Source Ref Nr " },
        { title: "Justice Nr    " },
        { title: "Status        ", className: "text-left" },
        { title: "State         ", className: "text-left" }
    ];

    DataTableManager.Setup(columnData);

});