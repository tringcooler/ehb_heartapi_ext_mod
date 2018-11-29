using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Metadata;
using Mono.Collections.Generic;

namespace t01
{
    class Program
    {
        private static MethodDefinition clone_method(MethodDefinition src, string dname)
        {
            MethodDefinition dst = new MethodDefinition(dname, src.Attributes, src.ReturnType);
            dst.DeclaringType = src.DeclaringType;
            ILProcessor dil = dst.Body.GetILProcessor();
            foreach(ParameterDefinition par in src.Parameters) {
                dst.Parameters.Add(par);
            }
            foreach(VariableDefinition va in src.Body.Variables) {
                dst.Body.Variables.Add(va);
            }
            foreach(Instruction ins in src.Body.Instructions) {
                Instruction dins;
                if(ins.Operand is null) {
                    dins = dil.Create(ins.OpCode);
                } else {
                    var _cins = dil.GetType().GetMethod("Create", new Type[] {ins.OpCode.GetType(), ins.Operand.GetType()});
                    dins = (Instruction)_cins.Invoke(dil, new object[] {ins.OpCode, ins.Operand});
                }
                dil.Append(dins);
            }
            return dst;
        }

        private static MethodReference clone_opr_mr(object osrc) {
            MethodReference src = (MethodReference)osrc;
            MethodReference dst = new MethodReference(src.Name, src.ReturnType, src.DeclaringType);
            dst.HasThis = src.HasThis;
            dst.CallingConvention = src.CallingConvention;
            //dst.ExplicitThis = src.ExplicitThis;
            //dst.MetadataToken = src.MetadataToken;
            foreach(ParameterDefinition par in src.Parameters) {
                dst.Parameters.Add(par);
            }
            foreach(GenericParameter par in src.GenericParameters) {
                dst.GenericParameters.Add(par);
            }
            return dst;
        }

        private static MethodReference clone_opr_gmr(object osrc) {
            MethodReference src = (MethodReference)osrc;
            if(src.IsGenericInstance) {
                GenericInstanceMethod g_src = (GenericInstanceMethod)src;
                src = g_src.GetElementMethod();
                MethodReference dst = clone_opr_mr(src);
                GenericInstanceMethod g_dst = new GenericInstanceMethod(dst);
                foreach(TypeReference par in g_src.GenericArguments) {
                    g_dst.GenericArguments.Add(par);
                }
                return g_dst;
            } else {
                return clone_opr_mr(src);
            }
        }

        private static MethodDefinition tp_get_method(ModuleDefinition mod, MethodDefinition src, string hook, string name, TypeReference tp) {
            MethodDefinition dst = clone_method(src, name);
            if(dst.ReturnType != mod.TypeSystem.Void) {
                dst.ReturnType = tp;
            } else if(tp != mod.TypeSystem.Int32) {
                ParameterDefinition s_par = dst.Parameters[2];
                ParameterDefinition d_par = new ParameterDefinition(s_par.Name, s_par.Attributes, tp);
                dst.Parameters[2] = d_par;
            }
            ILProcessor il = dst.Body.GetILProcessor();
            Instruction s_ins = dst.Body.Instructions
				.Single(i =>
					i.OpCode == OpCodes.Callvirt
					&& ((MethodReference) i.Operand).Name == hook);
            GenericInstanceMethod g_opr = (GenericInstanceMethod)clone_opr_gmr(s_ins.Operand);
            g_opr.GenericArguments[0] = tp;
            Instruction d_ins = il.Create(s_ins.OpCode, g_opr);
            il.Replace(s_ins, d_ins);
            if(dst.ReturnType != mod.TypeSystem.Void) {
                il.Remove(dst.Body.Instructions[dst.Body.Instructions.Count - 3]);
                il.Remove(dst.Body.Instructions[dst.Body.Instructions.Count - 2]);
            } else if(tp == mod.TypeSystem.Boolean) {
                Instruction _t_ins = dst.Body.Instructions.Single(i => i.OpCode == OpCodes.Box);
                il.Replace(_t_ins.Previous, il.Create(OpCodes.Ldarga, 3));
                MethodReference _t_mr = new MethodReference("ToString", mod.TypeSystem.String, mod.TypeSystem.Boolean);
                _t_mr.HasThis = true;
                il.Replace(_t_ins, il.Create(OpCodes.Callvirt, _t_mr));
            } else if(tp != mod.TypeSystem.Int32) {
                il.Remove(dst.Body.Instructions.Single(i => i.OpCode == OpCodes.Box));
            } else if(tp == mod.TypeSystem.Int32){
                il.InsertBefore(d_ins, il.Create(OpCodes.Conv_I4));
            }
            return dst;
        }

        private static void insert_print(ModuleDefinition mod, ILProcessor il, Instruction fst, string s) {
            il.InsertBefore(fst, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(fst, il.Create(OpCodes.Ldfld,
                new FieldReference("_computer", mod.GetType("GameWorld2.Computer"), mod.GetType("GameWorld2.HeartAPI"))));
            il.InsertBefore(fst, il.Create(OpCodes.Ldstr, s));
            var mprnt =  new MethodReference("API_Print", mod.TypeSystem.Void, mod.GetType("GameWorld2.Computer"));
            mprnt.HasThis = true;
            mprnt.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
            il.InsertBefore(fst, il.Create(OpCodes.Callvirt, mprnt));
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!" + true);

            const string dst_file = "G:/Steam/steamapps/common/ElseHeartbreak/ElseHeartbreak_Data/Managed/GameWorld2_bak.dll";
            const string out_file = "G:/Steam/steamapps/common/ElseHeartbreak/ElseHeartbreak_Data/Managed/GameWorld2.dll";
            ModuleDefinition _module = ModuleDefinition.ReadModule(dst_file);

            //foreach(TypeReference r in _module.GetTypeReferences())Console.WriteLine(r);
            //Console.WriteLine(_module.GetType("GameWorld2.Computer").GetType());

            var target_type = _module.Types.Single(t => t.Name == "HeartAPI");
            //target_type.Module.ImportReference(_module.Types.Single(t => t.Name == "Computer").Methods.Single(m => m.Name == "API_Print"));
			
            /*dst_method = clone_method(dst_method, "API_GetXData");
            target_type.Methods.Add(dst_method);

            //var typepar = new GenericParameter(dst_method);
            //dst_method.GenericParameters.Add(typepar);
            //dst_method.ReturnType = new GenericParameter(dst_method);

            //var new_method = new GenericInstanceMethod(dst_method);
            //var nm_rtyp = new GenericParameter("T", new_method);
            //new_method.ReturnType = nm_rtyp;
            //new_method.GenericArguments.Add(nm_rtyp);
            //var _t = new_method.GetElementMethod();

            var nm_rtyp = new GenericParameter("T", dst_method);
            dst_method.ReturnType = nm_rtyp;
            dst_method.GenericParameters.Add(nm_rtyp);

            var padpar = new ParameterDefinition("pad", ParameterAttributes.None, nm_rtyp);
            dst_method.Parameters.Add(padpar);

            //target_type.Methods.Remove(dst_method);

            var il = dst_method.Body.GetILProcessor();
            
            var call_instr = dst_method
				.Body
				.Instructions
				.Single(i =>
					i.OpCode == OpCodes.Callvirt
					&& ((MethodReference) i.Operand).Name == "GetValue");
            
            foreach (var ins in dst_method.Body.Instructions)
                Console.WriteLine(ins);
            
            MethodReference opr = (MethodReference)call_instr.Operand;
            MethodReference nopr = clone_opr_mr(opr);
            //var vartype = _module.ImportReference(typeof(VariantType));
            //opr.ReturnType = _module.TypeSystem.Var
            //opr.ReturnType = _module.TypeSystem.String;
            //Console.WriteLine(opr.ReturnType);
            //opr.MethodReturnType = _module.TypeSystem.String;
            
            //GenericInstanceMethod oprg = (GenericInstanceMethod)opr;
            GenericInstanceMethod oprg = new GenericInstanceMethod(nopr);
            oprg.GenericArguments.Add(nm_rtyp);
            oprg.ReturnType = nm_rtyp;
            Console.WriteLine(oprg.GenericArguments[0]);
            var new_instr =  il.Create(call_instr.OpCode, oprg);
            Console.WriteLine(call_instr);
            Console.WriteLine(new_instr);
            il.Replace(call_instr, new_instr);

            //var mtd = oprg.GetElementMethod();
            //Console.WriteLine(mtd);
            //Console.WriteLine(call_instr);
            
            //var new_instr = il.Create(OpCodes.Callvirt, opr);
            //il.Replace(call_instr, new_instr);
            //il.Remove(call_instr);
            //target_type.Methods.Remove(dst_method);

            //dst_method.ReturnType = vartype;
            
            //return;*/

            var dst_method = target_type.Methods.Single(m => m.Name == "API_GetNumericData");
            target_type.Methods.Add(tp_get_method(_module, dst_method, "GetValue", "API_GetIntData", _module.TypeSystem.Int32));
            target_type.Methods.Add(tp_get_method(_module, dst_method, "GetValue", "API_GetStringData", _module.TypeSystem.String));
            target_type.Methods.Add(tp_get_method(_module, dst_method, "GetValue", "API_GetBoolData", _module.TypeSystem.Boolean));

            //var dil = dst_method.Body.GetILProcessor();
            /*var dins1 = dst_method.Body.Instructions.Single(i =>
					i.OpCode == OpCodes.Callvirt
					&& ((MethodReference) i.Operand).Name == "GetValue");
            ((GenericInstanceMethod)dins1.Operand).GenericArguments[0] = _module.ImportReference(typeof(System.Boolean));*/
            //dil.Remove(dins1);
            //var fst_ins = dst_method.Body.Instructions[0];
            //dil.InsertBefore(fst_ins, dil.Create(OpCodes.Ldc_R4, (float)123.0));
            //dil.InsertBefore(fst_ins, dil.Create(OpCodes.Ret));
            
            //foreach(var r in dst_method.Module.GetTypeReferences()) Console.WriteLine(r);
            //insert_print(_module, dil, fst_ins, "here1");

            /*for(var i = 0; i < dst_method.Body.Instructions.Count; i++) {
                Console.WriteLine(i);
                Console.WriteLine(dst_method.Body.Instructions[i]);
            }
            var _rms = 16;// + 4;
            for(var i = _rms; i < _rms + 8; i++) {
                dil.Remove(dst_method.Body.Instructions[_rms]);
            }
            dil.InsertAfter(dst_method.Body.Instructions[_rms - 1], dil.Create(OpCodes.Ldc_R4, (float)123.0));*/
            //((Instruction)dst_method.Body.Instructions[10].Operand).Offset += 16;

            /*dil.InsertBefore(dst_method.Body.Instructions[dst_method.Body.Instructions.Count - 1], dil.Create(OpCodes.Ldc_R4, (float)123.0));
            dil.Replace(dst_method.Body.Instructions[dst_method.Body.Instructions.Count - 5],
                dil.Create(OpCodes.Pop));
            dil.Replace(dst_method.Body.Instructions[dst_method.Body.Instructions.Count - 4],
                dil.Create(OpCodes.Pop));
            dil.Remove(dst_method.Body.Instructions[dst_method.Body.Instructions.Count - 3]);*/
            /*dil.Replace(dst_method.Body.Instructions[dst_method.Body.Instructions.Count - 3],
                dil.Create(OpCodes.Pop));
            dil.Replace(dst_method.Body.Instructions[dst_method.Body.Instructions.Count - 2], dil.Create(OpCodes.Ldc_R4, (float)123.0));*/

            dst_method = target_type.Methods.Single(m => m.Name == "API_SetNumericData");
            target_type.Methods.Add(tp_get_method(_module, dst_method, "SetValue", "API_SetIntData", _module.TypeSystem.Int32));
            target_type.Methods.Add(tp_get_method(_module, dst_method, "SetValue", "API_SetStringData", _module.TypeSystem.String));
            target_type.Methods.Add(tp_get_method(_module, dst_method, "SetValue", "API_SetBoolData", _module.TypeSystem.Boolean));

            _module.Write(out_file);

            /*_module = ModuleDefinition.ReadModule(out_file);
            try {
                Console.WriteLine(_module.Types.Single(t => t.Name == "HeartAPI").Methods.Single(m => m.Name == "API_GetXData").Body
				.Instructions
				.Single(i =>
					i.OpCode == OpCodes.Callvirt
					&& ((MethodReference) i.Operand).Name == "GetValue"));
            } catch {
                Console.WriteLine("none");
            }*/
        }
    }
}
