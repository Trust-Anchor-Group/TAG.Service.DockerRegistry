Title: Docker Repository
Master: /Master.md
JavaScript: /Events.js
JavaScript: /TargetBlank.js
UserVariable: User
Script: /Controls/SimpleTable.script
Privilege: DockerRegistry
Login: /Login.md
CSS: Style.cssx
Parameter: guid

{{  
Repo := select top 1 * from DockerRepository where Guid=guid;

if Repo = null then
    NotFound("Repository with object guid " + guid + " does not exist.");

DockerDashboardAssertPermisions(Repo, "DockerRegistry.Read");

if exists(Posted) then
(
    // delete repository
    if Posted matches { "delete": Bool(PDelete) } and PDelete then
    (
        DockerDashboardAssertPermisions(Repo, "DockerRegistry.Delete");
        DeleteObject(Repo);
		TemporaryRedirect("Repositories.md");
    );

    // delete image reference
    if Posted matches { "deleteImage": Bool(PDeleteImage), "tag": String(PTag) } and PDeleteImage then
    (
        DockerDashboardAssertPermisions(Repo, "DockerRegistry.Delete");
        if DockerDeleteImageReference(Repo, PTag) then
            ]] +> Deleted image reference ((Repo.RepositoryName)):((PTag)) [[
        else
            ]] > No image reference with tag ((PTag)) exists [[;
    );

    // update visibility
    if Posted matches { "setVisibility": Bool(PSetVisibility), "visibility": PVisibility } then (
        DockerDashboardAssertPermisions(Repo, "DockerRegistry.Update");
        Repo.IsPrivate:= not (PVisibility = "public");
        UpdateObject(Repo);
    );

    // add whitelist
    if Posted matches { "addToWhitelist": Bool(PAddToWhitelist), "accountName": PAccountName } and PAddToWhitelist then (
        DockerUser := select top 1 * from TAG.Networking.DockerRegistry.Model.DockerUser where AccountName=PAccountName;
        if DockerUser = null then (
            ]] > No user with account name ((PAccountName)) exists [[;
        ) else (
            DockerDashboardAssertPermisions(Repo, "DockerRegistry.Update");
            Repo.CreatePrivileges(DockerUser, true, true);
            ]] +> Whitelisted user ((PAccountName)) [[;
        )
    );

    // remove whitelist
    if Posted matches { "removeFromWhitelist": Bool(PRemoveFromWhitelist), "accountGuid": PAccountGuid } and PRemoveFromWhitelist then (
        DockerUser := select top 1 * from TAG.Networking.DockerRegistry.Model.DockerUser where Guid=PAccountGuid;
        Pri := select top 1 * from TAG.Networking.DockerRegistry.Model.DockerRepositoryPrivilege where ActorGuid=PAccountGuid;
        if DockerUser = null or Pri = null then (
            ]] > No user with guid ((PAccountGuid)) is whitelisted [[;
        ) else (
            DockerDashboardAssertPermisions(Repo, "DockerRegistry.Update");
            DeleteObject(Pri);
            ]] +> Removed user ((DockerUser.AccountName)) whitelist privileges [[;
        )
    );
);
}}

================================================================================================================================
	
# Docker Repository: {{Repo.RepositoryName}}



==================================================

<div class="docker-row">
<div>
{{
    Owner := select top 1 * from DockerActor where Guid=Repo.OwnerGuid;
    if Owner = null then (
        ]] > No owner [[;
    ) else (
        if Owner is TAG.Networking.DockerRegistry.Model.DockerUser then
            ]] <h2>Owner</h2> User: [((Owner.AccountName))](DockerUser.md?guid=((Owner.Guid))) [[
        else 
            ]] <h2>Owner</h2> Organization: [((Owner.OrganizationName))](DockerOrganization.md?guid=((Owner.Guid))) [[
    );
}}
</div>
<form method="POST">  
    <div>
        <label for="visibility">Visibility</label>
        <select name="visibility" id="visibility">
            <option value="public" {{if not Repo.IsPrivate then "selected" else ""}}>Public</option>
            <option value="private" {{if Repo.IsPrivate  then "selected" else ""}}>Private</option>
        </select>
    </div>
    <input name="setVisibility" value="true" hidden>
    <button>Update visibility</button>
</form>
</div>

============================================================================

## User Whitelist

{{
	Privileges := select * from DockerRepositoryPrivilege where RepositoryGuid=Repo.Guid;

    foreach Pri in Privileges do (
        Account := select top 1 * from TAG.Networking.DockerRegistry.Model.DockerUser where Guid=Pri.ActorGuid;
        if not (Account = null) then (
            ]]
            <form method="POST" class="docker-row">
                <div>
                    <label for="accountGuid">Account name: ((Account.AccountName))</label>
                    <input type="text" name="accountGuid" id="accountGuid" value="((Account.Guid))") hidden>
                </div>
                <input name="removeFromWhitelist" value="true" hidden>
                <button class="negButton">Remove whitelist</button>
            </form>
            [[;
        )
    );
}}

<form method="POST" class="docker-row">
    <div>
        <label for="accountName">Account name</label>
        <input type="text" name="accountName" id="accountName" value="" required>
    </div>
    <input name="addToWhitelist" value="true" hidden>
    <button>Add to whitelist</button>
</form>

==================================================

## Images

{{
CanDeleteImages:=DockerDashboardHasPermisions(Repo, "DockerRegistry.Delete");

PrepareTable(()->(
    Page.Order:="Tag";
    Repo.GetManifests()
));
}}

<table>
    <thead>
        <tr>
            <th>{{Header("Name", "Name")}}</th>
            <th>{{Header("Tag", "Tag")}}</th>
            <th>{{Header("Digest", "Digest")}}</th>
            <th>{{Header("Size", "Size")}}</th>
            <th>Actions</th>
        </tr>
    </thead>
    <tbody>
{{
foreach Image in Page.Table do
(
    ]]
        <tr>
            <td>((Image.RepositoryName)):((Image.Tag))</td>
            <td>((Image.Tag))</td>
            <td>((Image.Digest))</td>
            <td>((ToMetricBytes(Image.Size);))</td>
            <td>
    [[;
    if CanDeleteImages and not (Image.Tag = "_") then (
        ]]
                <form method="POST" onsubmit="DockerAreYouSure(event, 'Are you sure you want to delete this image reference?')">
                    <input name="tag" value="((Image.Tag))" hidden>
                    <input name="deleteImage" value="true" hidden>
                    <button class="negButton">Delete</button>
                </form>
        [[;
    );
    ]]
            </td>
        </tr>
    [[;
)
}}
    </tbody>
</table>

============================================================================

<div class="docker-row">
	<form method="POST" onsubmit="DockerAreYouSure(event, 'Are you sure you want to delete this repository and all its images?')">
		<input name="delete" value="true" hidden>
		<button class="negButton">Delete Repository</button>
	</form>
	<div>
		<p><small>Repository GUID: {{Repo.Guid}}</small></p>
	</div>
</div>