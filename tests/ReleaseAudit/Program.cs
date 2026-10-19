using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

try {
 using var stream=File.OpenRead(args[0]);using var pe=new PEReader(stream);var metadata=pe.GetMetadataReader();
 if(metadata.GetString(metadata.GetAssemblyDefinition().Name)!="ChainsawPrototype")throw new Exception("Legacy assembly identity changed.");
 foreach(var reference in metadata.AssemblyReferences){var name=metadata.GetString(metadata.GetAssemblyReference(reference).Name);if(name.Contains("BloodFX",StringComparison.OrdinalIgnoreCase)||name.Contains("Adjuster",StringComparison.OrdinalIgnoreCase)||name.Contains("Inspector",StringComparison.OrdinalIgnoreCase))throw new Exception("Optional/private assembly became a hard dependency: "+name);}
 TypeDefinition? plugin=null;
 string[] forbidden={"Spear","DrawWeaponPackPane","PreparePreviewPane","PreviewPaneLayout","ReservePreview"};
 foreach(var handle in metadata.TypeDefinitions){
  var type=metadata.GetTypeDefinition(handle);var name=metadata.GetString(type.Name);
  if(name.Contains("Spear",StringComparison.OrdinalIgnoreCase))throw new Exception("Development weapon type included: "+name);
  if(metadata.GetString(type.Namespace)=="ChainsawPrototype"&&name=="Plugin")plugin=type;
  foreach(var methodHandle in type.GetMethods()){
   string method=metadata.GetString(metadata.GetMethodDefinition(methodHandle).Name);
   if(forbidden.Skip(1).Contains(method))throw new Exception("Obsolete settings override included: "+method);
  }
 }
 if(!plugin.HasValue)throw new Exception("Plugin type missing.");
 bool found=false;
 foreach(var handle in plugin.Value.GetCustomAttributes()){
  var attribute=metadata.GetCustomAttribute(handle);if(attribute.Constructor.Kind!=HandleKind.MemberReference)continue;
  var constructor=metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);if(constructor.Parent.Kind!=HandleKind.TypeReference)continue;
  var type=metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
  if(metadata.GetString(type.Namespace)!="BepInEx"||metadata.GetString(type.Name)!="BepInPlugin")continue;
  var blob=metadata.GetBlobReader(attribute.Value);if(blob.ReadUInt16()!=1)throw new Exception("Invalid plugin metadata.");
  string? guid=blob.ReadSerializedString(),name=blob.ReadSerializedString(),version=blob.ReadSerializedString();
  if(guid!="apocalypter.weaponspack"||name!="Apocalypter Weapons Pack"||version!=args[1])throw new Exception("Unexpected plugin identity/version.");found=true;
 }
 if(!found)throw new Exception("BepInPlugin metadata missing.");
 Console.WriteLine("PASS: release identity/version, no development weapon types and no obsolete settings-pane override.");
} catch(Exception error){Console.Error.WriteLine("FAIL: "+error.Message);Environment.ExitCode=1;}
